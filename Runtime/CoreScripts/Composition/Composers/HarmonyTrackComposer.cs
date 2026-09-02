using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;
using DryWetMidiNote = Melanchall.DryWetMidi.MusicTheory.Note;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// HarmonyTrackComposer
    /// 
    /// Goal (MVP):
    /// - Reads a guide melody line for this Part from the GenContext melody cache.
    ///   MGP-ALWTTT-HARMONY-1 (D-H1-5a=B): the composer's OWN musician is tried first
    ///   (self-harmony is the consumer's normal case), then the first cached melody in
    ///   the Part (track-list order, see <see cref="ResolveGuideMelody"/>).
    /// - Reads (or builds) the chord progression for this part.
    /// - For each melody GuideNote, picks a harmony pitch using IHarmonyStrategy
    ///   against the chord ACTUALLY sounding at that instant (canonical
    ///   <see cref="ChordProgressionData.FindChordEventAt"/>, accidental-aware).
    /// - Emits a second melodic line on the same timing grid as the melody, in the
    ///   PART's beat unit (MEL-BEATUNIT-1 via <see cref="MelodyTrackComposer.BeatsToSpan"/>).
    /// - Caches that harmony line back into the context as its own "melody"
    ///   under this track's MusicianId so other tracks (e.g. 3rd voice) can use it.
    ///
    /// Determinism: the note-resolution seam <see cref="ResolveHarmonyNotesCore"/> is a
    /// pure function of its inputs; the two shipped strategies draw no RNG. The only
    /// RNG consumer is the procedural-progression fallback (no cached progression).
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

            // 2. Figure out whose melody we're harmonizing (D-H1-5a=B: self first).
            var guideMelody = ResolveGuideMelody(ctx, part, cfg?.MusicianId, out var targetId);

            if (guideMelody == null || guideMelody.Count == 0)
            {
                Debug.LogWarning($"[HarmonyTrackComposer] " +
                                 $"No captured melody in GenContext for part '{part.Name}' " +
                                 $"(target='{targetId ?? "none"}'), skipping harmony.");
                return new MidiFile();
            }

            if (_settings?.logGenerator == true)
            {
                bool self = !string.IsNullOrEmpty(cfg?.MusicianId) && targetId == cfg.MusicianId;
                Debug.Log($"[HarmonyTrackComposer] part='{part.Name}' mus='{cfg?.MusicianId}' " +
                          $"harmonizing melody of '{targetId}' ({(self ? "self" : "other")}), " +
                          $"guideNotes={guideMelody.Count}");
            }

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
        /// MGP-ALWTTT-HARMONY-1 (D-H1-5a=B). Guide-melody target resolution:
        ///   1. this track's own musician, if the cache holds a non-empty melody for it
        ///      (exact-key lookup: no dependence on cache enumeration order);
        ///   2. otherwise <c>GetFirstMelodyMusicianIdForPart</c> — the first Melody track
        ///      in track-LIST order that published notes (Pass 1 composes in list order;
        ///      Dictionary enumeration follows insertion order for the insert-only cache).
        /// Returns null when nothing usable is cached. <paramref name="targetId"/> reports
        /// which musician was chosen (or null).
        /// </summary>
        public static List<MidiGenerator.GuideNote> ResolveGuideMelody(
            MidiGenerator.GenContext ctx,
            SongConfig.PartConfig part,
            string ownMusicianId,
            out string targetId)
        {
            targetId = null;
            if (ctx == null) return null;

            if (!string.IsNullOrEmpty(ownMusicianId))
            {
                var own = ctx.GetMelodyForPartMusician?.Invoke(part, ownMusicianId);
                if (own != null && own.Count > 0)
                {
                    targetId = ownMusicianId;
                    return own;
                }
            }

            var firstId = ctx.GetFirstMelodyMusicianIdForPart?.Invoke(part);
            if (string.IsNullOrEmpty(firstId)) return null;

            targetId = firstId;
            var first = ctx.GetMelodyForPartMusician?.Invoke(part, firstId);
            return (first != null && first.Count > 0) ? first : null;
        }

        /// <summary>A single resolved harmony note — the deterministic output of
        /// <see cref="ResolveHarmonyNotesCore"/>, ready to render via PatternBuilder.
        /// Timing is in PART beat units (same as the GuideNote it follows).</summary>
        public readonly struct ResolvedHarmonyNote
        {
            public readonly DryWetMidiNote Note;
            public readonly double WhenBeats;
            public readonly double DurBeats;

            public ResolvedHarmonyNote(DryWetMidiNote note, double whenBeats, double durBeats)
            {
                Note = note;
                WhenBeats = whenBeats;
                DurBeats = durBeats;
            }
        }

        /// <summary>
        /// Pure note-resolution seam (byte-identical to the render loop in
        /// <see cref="ComposeHarmonyFromMelody"/>; test target).
        ///
        /// For each GuideNote (Part beat units):
        ///  - convert its onset to absolute ticks with the PART beat span
        ///    (MGP-ALWTTT-HARMONY-1 item 1 / F-HARM-1: <see cref="MelodyTrackComposer.BeatsToSpan"/>,
        ///    never MusicalTimeSpan.Quarter — 6/8 guide beats are eighths);
        ///  - look up the chord sounding at that tick with the canonical
        ///    <see cref="ChordProgressionData.FindChordEventAt"/> (item 2 / F-HARM-3: Floor,
        ///    [startStep, startStep+lengthSteps) window, defined wrap);
        ///  - apply <c>degreeAccidental</c> to the degree root (item 2 / F-HARM-2, same law as
        ///    Backing/Bass/Melody: identity when 0);
        ///  - ask the strategy for a harmony pitch (null => rest, resets voice-leading memory).
        /// Because emission uses the same BeatsToSpan on the same beats, the lookup tick is
        /// exactly the tick the harmony note will sound at.
        /// </summary>
        public static List<ResolvedHarmonyNote> ResolveHarmonyNotesCore(
            IReadOnlyList<MidiGenerator.GuideNote> guideMelody,
            ChordProgressionData prog,
            Tonality tonality,
            Melanchall.DryWetMidi.MusicTheory.NoteName rootNote,
            TimeSignature timeSignature,
            TempoMap tempoMap,
            IHarmonyStrategy strategy,
            HarmonicLeadingConfig cfg,
            MIDIInstrumentSO instrument,
            System.Random rng)
        {
            var result = new List<ResolvedHarmonyNote>();
            if (guideMelody == null || guideMelody.Count == 0) return result;
            if (prog == null || prog.events == null || prog.events.Count == 0) return result;
            if (strategy == null || tempoMap == null) return result;

            // Tonal info used to translate progression degrees -> actual chord tone names.
            var scale = GetScaleFromTonality(tonality, rootNote);
            var scaleNames =
                GetNotesFromScale(scale, rootNote, 4, 7).Select(n => n.NoteName).ToArray();

            var beatSpan = GetBeatSpan(timeSignature);

            DryWetMidiNote lastHarmony = null;

            for (int i = 0; i < guideMelody.Count; i++)
            {
                var g = guideMelody[i];

                // Which chord event is active where this guide note sounds?
                long absTicks = TimeConverter.ConvertFrom(
                    MelodyTrackComposer.BeatsToSpan(g.startBeats, beatSpan), tempoMap);
                var chordEvt = prog.FindChordEventAt(tempoMap, timeSignature, absTicks);
                if (chordEvt == null)
                    continue;

                // Build the chord pitch classes for that event (accidental-aware).
                var degreeRoot = TransposeNoteName(
                    scaleNames[(int)chordEvt.degree], chordEvt.degreeAccidental);
                var chordPitchClasses = GetChordNoteNames(degreeRoot, chordEvt.quality);

                // Ask harmony strategy for the harmony pitch
                var harmonyNote = strategy.PickHarmony(
                    chordPitchClasses,
                    g.note,            // the melody pitch at this instant
                    lastHarmony,
                    instrument,
                    cfg,
                    rng
                );

                if (harmonyNote == null)
                {
                    // no harmony at this moment
                    lastHarmony = null;
                    continue;
                }

                result.Add(new ResolvedHarmonyNote(harmonyNote, g.startBeats, g.durBeats));
                lastHarmony = harmonyNote;
            }

            return result;
        }

        /// <summary>
        /// Core harmony writer: resolves the line via <see cref="ResolveHarmonyNotesCore"/>,
        /// emits it on the Part beat unit, stamps bank/patch, forces the channel, and
        /// publishes the line to the GenContext melody cache under THIS musician.
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
            if (prog?.events == null || prog.events.Count == 0)
                return new MidiFile();

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var beatSpan = GetBeatSpan(part.TimeSignature);

            var resolved = ResolveHarmonyNotesCore(
                guideMelody, prog, part.Tonality, part.RootNote, part.TimeSignature,
                tempoMap, _strategy, _cfg, instrument, rng);

            var pb = new PatternBuilder().MoveToStart();

            // collect the harmony line in beat space so future voices / doubling can reuse it.
            var capturedHarmony = new List<MidiGenerator.GuideNote>(resolved.Count);

            foreach (var r in resolved)
            {
                // Emit harmony note with (for now) a softer velocity than melody default.
                // Velocity policy is deferred (MGP-ALWTTT-HARMONY-1 item 7).
                var vel = (SevenBitNumber)80;

                // MGP-ALWTTT-HARMONY-1 item 1 (F-HARM-1): Part beat unit, not Quarter.
                var startTs = MelodyTrackComposer.BeatsToSpan(r.WhenBeats, beatSpan);
                var durTs = MelodyTrackComposer.BeatsToSpan(r.DurBeats, beatSpan);

                pb.MoveToTime(startTs);
                pb.Note(r.Note, durTs, vel);

                capturedHarmony.Add(new MidiGenerator.GuideNote
                {
                    startBeats = r.WhenBeats,
                    durBeats = r.DurBeats,
                    note = r.Note
                });
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

            // Cache this harmony line back into GenContext under THIS track's MusicianId,
            // so other voices (e.g. a 3rd harmony track) can harmonize *this* harmony.
            //
            // MGP-ALWTTT-HARMONY-1 (D-H1-5b=A, F-HARM-5 kept as-is, documented):
            // when this musician also holds the Melody (self-harmony), this REPLACES the
            // cache entry the Melody composer published under the same key. Benign because
            // (i) Harmony runs in PASS 2, the last pass, so no non-Harmony composer reads
            // the cache afterwards; (ii) it swaps the list REFERENCE — the Melody list is
            // never mutated, and the Melody MidiFile / stem (mus:{id}:Melody) was already
            // built before publication; (iii) the cache is re-created per repetition and
            // per single-part render, so nothing leaks across reps. Known edge (registered,
            // not Tier A): a second Harmony track for the same musician would follow this
            // harmony instead of the melody.
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
        /// Force every ChannelEvent in the file to a given MIDI channel.
        /// Same pattern used in MelodyTrackComposer and ChordTrackComposer.
        /// </summary>
        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        /// <summary>
        /// Stamp Bank Select + Program Change at the start of each track chunk.
        /// Mirrors MelodyTrackComposer.StampBankAndPatch.
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
        /// Light inspection logs similar to MelodyTrackComposer.Inspect.
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