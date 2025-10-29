using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// HarmonyTrackComposer
    /// 
    /// Goal (MVP):
    /// - Reads the lead melody line for a specific musician in this Part
    ///   from ctx.GetMelodyForPartMusician(part, targetMusicianId).
    /// - Reads (or builds) the chord progression for this part.
    /// - For each melody GuideNote, picks a harmony pitch using IHarmonyStrategy.
    /// - Emits a second melodic line (same timing grid as the melody).
    /// - Caches that harmony line back into the context as its own "melody"
    ///   under this track's MusicianId so other tracks (e.g. 3rd voice) can use it.
    /// </summary>
    public class HarmonyTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly HarmonicLeadingConfig _cfg;
        private readonly IHarmonyStrategy _strategy;

        public HarmonyTrackComposer(
            MidiGenPlayConfig settings,
            HarmonicLeadingConfig cfg,
            IHarmonyStrategy strategy = null)
        {
            _settings = settings;
            _cfg = cfg;
            _strategy = strategy ?? new NearestChordToneHarmonyStrategy();
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var instrument = (MIDIInstrumentSO)cfg.Instrument;
            if (instrument == null)
            {
                Debug.LogWarning("[HarmonyTrackComposer] Missing harmony instrument.");
                return new MidiFile();
            }

            var rng = ctx?.rng ?? new System.Random();

            // 1. Get or build chord progression for this part
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                    ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null)
            {
                prog = ChordTrackComposer.BuildProceduralProgression(part, ctx, rng);
                ctx?.SetProgressionForPart?.Invoke(part, prog);

                if (_settings?.logGenerator == true)
                {
                    Debug.Log($"[HarmonyTrackComposer] " +
                        $"Built & cached procedural progression for part '{part.Name}'.");
                }
            }
            else if (_settings?.logGenerator == true)
            {
                var seq = string.Join("  ", 
                    prog.events.Select(e => ToRomanRich(e.degree, e.quality)));
                Debug.Log($"[HarmonyTrackComposer] Using cached/authored progression: {seq}");
            }

            // 2. Figure out whose melody we're harmonizing.
            // Ask GenContext which musician has the first available melody for this Part.
            var targetId = ctx?.GetFirstMelodyMusicianIdForPart?.Invoke(part);

            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning("[HarmonyTrackComposer] " +
                    "No available melody in GenContext for this part, skipping harmony.");
                return new MidiFile();
            }

            var guideMelody = ctx?.GetMelodyForPartMusician?.Invoke(part, targetId);
            if (guideMelody == null || guideMelody.Count == 0)
            {
                Debug.LogWarning($"[HarmonyTrackComposer] " +
                                $"No captured melody for part '{part.Name}' " +
                                $"from musician '{targetId}', skipping harmony.");
                return new MidiFile();
            }

            // TODO: Obtain and use Tonality in pipeline too

            // 3. Compose harmony line aligned to that melody and progression.
            var file = ComposeHarmonyFromMelody(
                instrument,
                bpm,
                part,
                cfg,
                prog,
                guideMelody,
                channel,
                ctx,
                rng
            );

            return file;
        }

        /// <summary>
        /// Core harmony writer.
        /// For each GuideNote in the lead melody (startBeats, durBeats, melody pitch),
        /// we:
        ///   - find the active chord at that beat,
        ///   - ask IHarmonyStrategy for a harmony pitch,
        ///   - emit that harmony note (same timing),
        ///   - store it to capturedHarmony so we can save it in ctx.
        /// </summary>
        private MidiFile ComposeHarmonyFromMelody(
            MIDIInstrumentSO instrument,
            int bpm,
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig trackCfg,
            ChordProgressionData prog,
            List<MidiGenerator.GuideNote> guideMelody,
            int channel,
            MidiGenerator.GenContext ctx,
            System.Random rng)
        {
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder().MoveToStart();

            // Tonal info used to translate progression degrees -> actual chord tone names.
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames =
                GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            // We iterate chord events in time order for easy lookup.
            var evts = (prog.events ?? new List<ChordProgressionData.ChordEvent>())
                        .OrderBy(e => e.startStep)
                        .ToList();

            if (evts.Count == 0)
                return new MidiFile();

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // collect the harmony line in beat space so future voices / doubling can reuse it.
            var capturedHarmony = new List<MidiGenerator.GuideNote>();

            DryWetMidiNote lastHarmony = null;

            foreach (var g in guideMelody)
            {
                // Which chord event is active at this guide note's start?
                var chordEvt = FindEventAtBeat(evts, g.startBeats, stepsPerBeat);
                if (chordEvt == null)
                    continue;

                // Build the chord pitch classes for that event
                var degreeRoot = scaleNames[(int)chordEvt.degree];
                var chordPitchClasses = GetChordNoteNames(degreeRoot, chordEvt.quality);

                // Ask harmony strategy for the harmony pitch
                var harmonyNote = _strategy.PickHarmony(
                    chordPitchClasses,
                    g.note,            // the melody pitch at this instant
                    lastHarmony,
                    instrument,
                    _cfg,
                    rng
                );

                if (harmonyNote == null)
                {
                    // no harmony at this moment
                    lastHarmony = null;
                    continue;
                }

                // Emit harmony note with (for now) a softer velocity than melody default.
                // (Later this can come from HarmonicLeadingConfig e.g. backingVelMin/Max etc.)
                // TODO: get melofy velocity, use fraction eg 0.75
                var vel = (SevenBitNumber)80;

                var startTs = MusicalTimeSpan.Quarter.Multiply(g.startBeats);
                var durTs = MusicalTimeSpan.Quarter.Multiply(g.durBeats);

                pb.MoveToTime(startTs);
                pb.Note(harmonyNote, durTs, vel);

                // Track for call-and-response / future harmonies
                capturedHarmony.Add(new MidiGenerator.GuideNote
                {
                    startBeats = g.startBeats,
                    durBeats = g.durBeats,
                    note = harmonyNote
                });

                lastHarmony = harmonyNote;
            }

            var file = pb.Build().ToFile(tempoMap);

            // match the other composers: stamp patch/bank & force channel
            StampBankAndPatch(file, instrument, channel);
            ForceAllChannel(file, channel);

            if (_settings?.logGenerator == true)
            {
                var (tracks, notes, lastTick) = Inspect(file);
                Debug.Log($"[HarmonyTrackComposer] " +
                    $"tracks={tracks} notes={notes} lastTick={lastTick} " +
                    $"cachedHarmonyNotes={capturedHarmony.Count}");
            }

            // IMPORTANT:
            // Cache this harmony line back into GenContext under THIS track's MusicianId.
            // That means other voices (e.g. a 3rd harmony track) can harmonize *this* harmony.
            if (ctx != null && ctx.SetMelodyForPartMusician != null)
            {
                var thisMusician = trackCfg?.MusicianId;
                if (!string.IsNullOrEmpty(thisMusician))
                {
                    // NOTE: Stored as melody, not harmony
                    ctx.SetMelodyForPartMusician(part, thisMusician, capturedHarmony);
                }
            }

            return file;
        }


        /// <summary>
        /// Given an ordered list of ChordEvents (each with startStep / lengthSteps),
        /// find the one active at the given beat time within the part.
        /// </summary>
        private static ChordProgressionData.ChordEvent FindEventAtBeat(
            List<ChordProgressionData.ChordEvent> orderedEvents,
            double beat,
            int stepsPerBeat)
        {
            // convert beat position into "step" space (integer-ish grid of the progression)
            int stepPos = Mathf.RoundToInt((float)(beat * stepsPerBeat));

            ChordProgressionData.ChordEvent candidate = null;
            int best = int.MinValue;

            foreach (var ev in orderedEvents)
            {
                int s = Mathf.Max(0, ev.startStep);
                if (s <= stepPos && s >= best)
                {
                    candidate = ev;
                    best = s;
                }
            }

            return candidate ?? orderedEvents.FirstOrDefault();
        }

        /// <summary>
        /// Force every ChannelEvent in the file to a given MIDI channel.
        /// Same pattern used in MelodyTrackComposer and ChordTrackComposer. :contentReference[oaicite:1]{index=1} :contentReference[oaicite:2]{index=2}
        /// </summary>
        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        /// <summary>
        /// Stamp Bank Select + Program Change at the start of each track chunk.
        /// Mirrors MelodyTrackComposer.StampBankAndPatch. :contentReference[oaicite:3]{index=3}
        /// </summary>
        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[HarmonyTrackComposer] Instrument bank is not numeric: '{inst.BankName}'");
                bank = 0;
            }

            foreach (var chunk in file.GetTrackChunks())
            {
                var msb = (SevenBitNumber)bank;
                var lsb = (SevenBitNumber)0;

                chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, msb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, lsb)
                { Channel = (FourBitNumber)channel, DeltaTime = 0 });

                chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
                { Channel = (FourBitNumber)channel, DeltaTime = 1 });
            }
        }

        /// <summary>
        /// Light inspection logs similar to MelodyTrackComposer.Inspect. :contentReference[oaicite:4]{index=4}
        /// </summary>
        private static (int tracks, int notes, long lastTick) Inspect(MidiFile f)
        {
            if (f == null) return (0, 0, 0);
            var chunks = f.GetTrackChunks().ToList();
            var notes = f.GetNotes().Count();
            var last = chunks.SelectMany(c => c.GetTimedEvents())
                              .Select(te => te.Time).DefaultIfEmpty(0).Max();
            return (chunks.Count, notes, last);
        }

    }
}