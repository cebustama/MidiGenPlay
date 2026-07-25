using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using System;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.Composition.BasslineCardConfigSO;
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
    /// CA-V1: the bass now owns the Random roll too (D6 lifted) and carries the
    /// seeded velocity jitter. Both run on seed-derived substreams; ctx.rng and
    /// the note-selection draw order are untouched.
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

            // MGP-ALWTTT-DBG-3 (Ask C): a patternOverride targeting Bassline is
            // warn + ignore in v1. The bass owns no pattern channel — it renders
            // the per-part SHARED progression — so honoring an override here
            // would create a second mutation path into shared state. Override
            // the Backing track instead (its override IS shared, by design).
            if (ctx?.patternOverride != null)
            {
                Debug.LogWarning(
                    $"[BassTrackComposer] patternOverride targeting Bassline is not " +
                    $"supported in v1 (got '{ctx.patternOverride.name}'). The bass " +
                    $"renders the shared progression; override the Backing track " +
                    $"instead. Ignoring.");
            }

            // MGP-ALWTTT-DBG-1 (Ask A): source-tracked resolution — same
            // precedence as before (shared cache, else TrackParameters).
            var sharedProg = ctx?.GetProgressionForPart?.Invoke(part);
            var prog = sharedProg ?? (cfg.Parameters?.Pattern as ChordProgressionData);

            if (prog == null || prog.events == null || prog.events.Count == 0)
            {
                // Ask A: nothing rendered.
                ctx?.ReportResolved?.Invoke(new ResolvedTrackChoice
                {
                    source = ResolvedSource.None,
                    usesSharedProgression = false,
                });
                return new MidiFile();
            }

            // Ask A: the corrected bass payload — flag + shared progression
            // identity (roman formatted exactly like the backing readback).
            ctx?.ReportResolved?.Invoke(new ResolvedTrackChoice
            {
                source = sharedProg != null
                    ? ResolvedSource.SharedProgression
                    : ResolvedSource.TrackParameters,
                usesSharedProgression = sharedProg != null,
                sourceAssetName = prog.name,
                progressionRoman = ChordTrackComposer.RomanSequence(prog),
            });

            // CA-F2 (SD-F2-4=A / SD-F2-5=A / D-EXP1=A): persistent card-level
            // articulation selection, resolved once at entry from the track's
            // Style slot. No card (or a non-bass bundle) => Block, independent
            // of the backing card. No snapshot-and-clear: the §6/§7 transient
            // lifecycle does not apply.
            var (chordExpression, arpeggioRate) = ResolveArticulation(cfg);

            // CA-V1 (D-V1-BASS=B): the ARTIC-1 D6 limitation is LIFTED — the bass
            // now rolls its own figures/rates instead of degrading Random to
            // Block. Its substreams derive from the BASS trackSeed, which already
            // folds in role + musicianId (ResolveTrackSeed*), so backing and bass
            // on the same part never share a roll sequence.
            //
            // Critical: none of this touches ctx.rng. The note-selection loop
            // below keeps its exact per-event draw count and order (1 draw root
            // mode, 2 chord-tone mode) — the determinism surface of section 2 of
            // the Bass SSoT.
            var bassStyle = cfg?.Parameters?.Style as BasslineCardConfigSO;
            int trackSeed = ctx != null ? ctx.trackSeed : 0;
            var toneMode = bassStyle != null
                ? bassStyle.arpeggioToneMode : BassArpeggioToneMode.RepeatedNote;

            RandomArticulationRoller articRoller = null;
            if (chordExpression == ChordExpressionType.Random ||
                arpeggioRate == ArpeggioRate.Random)
            {
                articRoller = new RandomArticulationRoller(
                    new System.Random(SongOrchestrator.ResolveArticulationSeed(trackSeed)),
                    bassStyle != null ? bassStyle.randomRerollChance : 1f,
                    bassStyle != null ? bassStyle.randomFigureWeights : null,
                    new System.Random(SongOrchestrator.ResolveArticulationRateSeed(trackSeed)));
            }

            var velocityJitter = new VelocityJitter(
                bassStyle != null ? bassStyle.velocityJitter : 0,
                SongOrchestrator.ResolveVelocityJitterSeed(trackSeed));

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

            int eventIndex = 0;
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
                var effectiveExpression =
                    articRoller != null &&
                    chordExpression == ChordExpressionType.Random
                        ? articRoller.NextFigure() : chordExpression;
                var effectiveRate =
                    articRoller != null &&
                    arpeggioRate == ArpeggioRate.Random
                        ? articRoller.NextRate() : arpeggioRate;

                // BASS-WALK-1 (D-WALK-HOME=A / D-WALK-RNG=A): when the resolved figure is
                // an arpeggio and walk mode is on, hand the SAME Emit a root-anchored
                // triad and let the existing k % noteCount cycling do the walk. Zero new
                // ctx.rng draws: 3rd/5th are deterministic from chordPcs, stacked above
                // the already-drawn root octave. ArpeggioFits guards the degrade path so
                // a too-short event never emits a 3-note chord (mono invariant).
                NoteTheory[] playable;
                if (toneMode == BassArpeggioToneMode.ChordToneWalk &&
                    (effectiveExpression == ChordExpressionType.ArpeggioUp ||
                     effectiveExpression == ChordExpressionType.ArpeggioDown) &&
                    chordPcs.Length >= 2 &&
                    ChordArticulator.ArpeggioFits(lenBeats, effectiveRate))
                {
                    playable = BuildWalkVoicing(chordPcs, oct);
                }
                else
                {
                    playable = new[] { note };
                }

                _articulator.Emit(pb, playable, startBeats, lenBeats,
                                  beatSpan, beatsPerBar, ce.velocity, stepsPerBeat,
                                  effectiveExpression, effectiveRate,
                                  velocityJitter.ForEvent(eventIndex));

                eventIndex++;
            }

            var file = pb.Build().ToFile(tempoMap);

            if (_settings?.logGenerator == true)
            {
                var all = file.GetNotes().OrderBy(n => n.Time).ToList();
                Debug.Log($"[BASS-WALK probe2] notes={all.Count} " +
                          $"distinctPitches={all.Select(n => (int)n.NoteNumber).Distinct().Count()} " +
                          $"first12={string.Join(",", all.Take(12).Select(n => (int)n.NoteNumber))}");
            }

            // channel + program (match other composers)
            ForceAllChannel(file, channel);
            StampBankAndPatch(file, inst, channel);

            if (_settings?.logGenerator == true)
            {
                var notes = file.GetNotes().Count();
                var lastTick = file.GetTrackChunks().SelectMany(c => c.GetTimedEvents())
                                   .Select(te => te.Time).DefaultIfEmpty(0).Max();
                Debug.Log($"[BassTrackComposer] notes={notes} lastTick={lastTick} " +
                          $"expr={chordExpression} rate={arpeggioRate} " +
                          $"jitter={velocityJitter.Amount}" +
                          (articRoller != null
                              ? $" | CA-V1 roll {articRoller.DescribeRolls()}"
                              : ""));
            }

            return file;
        }

        /// <summary>
        /// BASS-WALK-1: root/3rd/5th (first Min(3, chordPcs.Length) tones) stacked
        /// strictly ascending from the drawn root octave — each tone placed in the
        /// nearest octave above the previous note. Deterministic; no rng.
        /// </summary>
        public static NoteTheory[] BuildWalkVoicing(NoteName[] chordPcs, int rootOct)
        {
            int count = Math.Min(3, chordPcs.Length);
            var notes = new NoteTheory[count];
            notes[0] = NoteTheory.Get(chordPcs[0], rootOct);
            for (int i = 1; i < count; i++)
            {
                var n = NoteTheory.Get(chordPcs[i], rootOct);
                if (n.NoteNumber <= notes[i - 1].NoteNumber)
                    n = NoteTheory.Get(chordPcs[i], rootOct + 1);
                notes[i] = n;
            }
            return notes;
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