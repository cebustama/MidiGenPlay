using System;
using System.Linq;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;
using NoteTheory = Melanchall.DryWetMidi.MusicTheory.Note;

namespace MidiGenPlay.Composition
{
    /// Minimal bass composer: an articulated monophonic line, one selected note
    /// per chord event. Mode: root-only (default) or random chord tone
    /// (constructor flag).
    ///
    /// CA-F2 (D-PRIO=A Feature 2): consumes the shared Tier-1 articulation
    /// engine (IChordArticulator, SD-F2-1=A) with a 1-note voicing per event.
    /// Block / no BasslineCardConfigSO is bit-identical to the legacy
    /// MoveToTime+Note pair (test-pinned). The note-selection loop — including
    /// its per-event ctx.rng draw sequence — is deliberately unchanged; only the
    /// emission pair was replaced. See runtime/SSoT_Composer_Bass_Track.md.
    public sealed class BassTrackComposer : ITrackComposer
    {
        private readonly MidiGenPlayConfig _settings;
        private readonly bool _randomChordTone;

        // CA-F2: shared Tier-1 articulation seam (the SAME engine the
        // ChordTrackComposer uses, D-PRIO=A). Stateless and RNG-free by
        // contract — it never consumes ctx.rng, so the bass's own per-event
        // draw sequence below is unaffected — hence a single shared instance.
        private static readonly IChordArticulator _articulator = new ChordArticulator();

        public BassTrackComposer(MidiGenPlayConfig settings, bool randomChordTone = false)
        {
            _settings = settings;
            _randomChordTone = randomChordTone;
        }

        public MidiFile Compose(
            SongConfig.PartConfig part,
            SongConfig.PartConfig.TrackConfig cfg,
            int bpm,
            int channel,
            MidiGenerator.GenContext ctx)
        {
            var inst = (MIDIInstrumentSO)cfg.Instrument;
            var prog = ctx?.GetProgressionForPart?.Invoke(part)
                       ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null || prog.events == null || prog.events.Count == 0)
                return new MidiFile();

            // CA-F2 (SD-F2-4=A / SD-F2-5=A / D-EXP1=A): persistent card-level
            // articulation selection, resolved once at entry from the track's
            // Style slot. No card (or a non-bass bundle) => Block, independent
            // of the backing card. No snapshot-and-clear: the §6/§7 transient
            // lifecycle does not apply.
            var (chordExpression, arpeggioRate) = ResolveArticulation(cfg);

            // CA-F2 (SD-F2-3=B): meter authority — derive the beat grid from the
            // Part TS, mirroring ChordTrackComposer. NOTE the recorded deviation:
            // legacy bass emitted on MusicalTimeSpan.Quarter unconditionally, so
            // in beat-unit != 4 meters (e.g. 6/8) it was desynced from the
            // backing track. Output is bit-identical in every beat-unit == 4
            // meter; in others this is a deliberate, test-pinned sync fix.
            var tsInfo = GetTimeSignatureDetails(part.TimeSignature, bpm);
            int beatsPerBar = tsInfo.BeatsPerMeasure;
            var beatSpan = GetBeatSpan(part.TimeSignature);

            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(bpm));
            var pb = new PatternBuilder();

            // scale → degree root names (for fast lookup)
            var scale = GetScaleFromTonality(part.Tonality, part.RootNote);
            var scaleNames = GetNotesFromScale(scale, part.RootNote, 4, 7).Select(n => n.NoteName).ToArray();

            int stepsPerBeat = Mathf.Max(1, prog.subdivisions);

            // bass register: favor lower region
            int minOct = Mathf.Max(0, inst.octaveMin - 1);
            int maxOct = Mathf.Max(minOct, inst.octaveMin + 1); // keep it low and stable

            var rng = ctx?.rng ?? new System.Random();

            foreach (var ce in prog.events.OrderBy(e => e.startStep))
            {
                var degreeRoot = scaleNames[(int)ce.degree];
                var chordPcs = GetChordNoteNames(degreeRoot, ce.quality);

                // choose pitch class: root or random chord tone
                var pc = _randomChordTone
                    ? chordPcs[rng.Next(0, chordPcs.Length)]
                    : chordPcs[0]; // root is first

                // pick octave in a narrow low band
                int oct = rng.Next(minOct, maxOct + 1);
                var note = NoteTheory.Get(pc, oct);

                // timings
                double startBeats = ce.startStep / (double)stepsPerBeat;
                double lenBeats = Math.Max(1, ce.lengthSteps) / (double)stepsPerBeat;

                // CA-F2: the bass's single emission site — one unconditional
                // articulator call (same anti-divergence discipline as the two
                // chord sites), replacing the legacy MoveToTime+Note pair.
                // SD-F2-1=A: a 1-note voicing through Emit; Block's 1-note
                // pb.Chord is byte-identical to the legacy pb.Note (test-pinned;
                // contingency on record: an EmitMono translator sharing PlanHits).
                // SD-F2-2=A: figures apply to the selected note; arpeggios become
                // a repeated-note pulse. Velocity note: Block clamps 0..127 where
                // legacy raw-cast threw out-of-range — byte-identical for valid
                // 0..127 data, strictly more robust otherwise.
                _articulator.Emit(pb, new[] { note }, startBeats, lenBeats,
                                  beatSpan, beatsPerBar, ce.velocity, stepsPerBeat,
                                  chordExpression, arpeggioRate);
            }

            var file = pb.Build().ToFile(tempoMap);

            // channel + program (match other composers)
            ForceAllChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            if (_settings?.logGenerator == true)
            {
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[BassTrackComposer] notes={notes} lastTick={lastTick} " +
                          $"expr={chordExpression} rate={arpeggioRate}");
            }

            return file;
        }

        /// <summary>
        /// CA-F2 articulation resolution (internal test seam, mirroring the
        /// ChordTrackComposer card-resolve pattern). SD-F2-4=A: the selection is
        /// a persistent field on <see cref="BasslineCardConfigSO"/> in the
        /// track's Style slot (D-EXP1=A). SD-F2-5=A: any other bundle type in
        /// the slot (including BackingCardConfigSO) resolves to the defaults, so
        /// an unset bass track is bit-identical regardless of what the backing
        /// track selects.
        /// </summary>
        public static (ChordExpressionType expression, ArpeggioRate rate)
            ResolveArticulation(SongConfig.PartConfig.TrackConfig cfg)
        {
            var style = cfg?.Parameters?.Style as BasslineCardConfigSO;
            return style != null
                ? (style.chordExpression, style.arpeggioRate)
                : (ChordExpressionType.Block, ArpeggioRate.Eighth);
        }

        private static void ForceAllChannel(MidiFile file, int channel)
        {
            foreach (var ev in file.GetTrackChunks().SelectMany(c => c.Events))
                if (ev is ChannelEvent ce) ce.Channel = (FourBitNumber)channel;
        }

        private static void StampBankAndPatch(MidiFile file, MIDIInstrumentSO inst, int channel)
        {
            var chunk = file.GetTrackChunks().FirstOrDefault();
            if (chunk == null)
            {
                chunk = new TrackChunk();
                file.Chunks.Add(chunk);
            }

            if (!int.TryParse(inst.BankName?.Trim(), out var bank))
            {
                Debug.LogWarning($"[BassTrackComposer] Instrument bank is not numeric: '{inst.BankName}', fallback to 0");
                bank = 0;
            }

            chunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)bank)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(1, new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)0)
            { Channel = (FourBitNumber)channel, DeltaTime = 0 });

            chunk.Events.Insert(2, new ProgramChangeEvent((SevenBitNumber)inst.PatchIndex)
            { Channel = (FourBitNumber)channel, DeltaTime = 1 });
        }
    }
}