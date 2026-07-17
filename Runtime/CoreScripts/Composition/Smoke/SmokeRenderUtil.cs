using System.Collections.Generic;
using System.Linq;
using System.Text;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Shared helpers for the Composition Smoke surfaces (editor window +
    /// runtime runner). Runtime-safe: no UnityEditor, no asset I/O.
    ///
    /// D-SMOKE-RT-2=B: <see cref="BuildEffectiveSpec"/> is the no-asset
    /// articulation fallback lifted verbatim from CompositionSmokeWindow so
    /// that the runtime runner mirrors the window exactly (same specs =>
    /// same bytes).
    /// D-SMOKE-RT-3=A: <see cref="StripMetronomeChunks"/> is the metronome
    /// strip lifted verbatim from the window; both consumers call it here.
    /// </summary>
    public static class SmokeRenderUtil
    {
        /// <summary>
        /// Returns the spec to hand to the assembler. When the D-SMOKE-MT-1=B
        /// fallback applies (no style asset, Backing/Bassline role), a fresh
        /// in-memory card SO carrying the given articulation is injected —
        /// persistent card-level surface (D-EXP1=A), never saved as an asset,
        /// lives only for this render (HideAndDontSave; not destroyed, tiny,
        /// dev-tool-acceptable). Other roles, or specs that already carry a
        /// Style asset, pass through untouched. Never mutates the input spec.
        /// </summary>
        public static SmokeTrackSpec BuildEffectiveSpec(
            SmokeTrackSpec s,
            ChordExpressionType chordExpression,
            ArpeggioRate arpeggioRate,
            float randomRerollChance = 1f,
            List<ChordExpressionWeight> randomFigureWeights = null)
        {
            if (s == null)
                return null;

            if (s.style != null ||
                (s.role != TrackRole.Backing && s.role != TrackRole.Bassline))
                return s;

            TrackStyleBundleSO inMem;
            if (s.role == TrackRole.Bassline)
            {
                var b = ScriptableObject.CreateInstance<BasslineCardConfigSO>();
                b.chordExpression = chordExpression;
                b.arpeggioRate = arpeggioRate;
                inMem = b;
            }
            else
            {
                var b = ScriptableObject.CreateInstance<BackingCardConfigSO>();
                b.chordExpression = chordExpression;
                b.arpeggioRate = arpeggioRate;
                // MGP-ALWTTT-ARTIC-1: inert unless chordExpression = Random.
                // Defaults (1f / empty) reproduce the shipped default policy
                // (per-chord roll, uniform six-figure pool).
                b.randomRerollChance = randomRerollChance;
                b.randomFigureWeights = randomFigureWeights != null
                    ? new List<ChordExpressionWeight>(randomFigureWeights)
                    : new List<ChordExpressionWeight>();
                inMem = b;
            }
            inMem.hideFlags = HideFlags.HideAndDontSave;

            // Do not mutate the caller's spec — copy with the in-memory
            // style attached.
            return new SmokeTrackSpec
            {
                role = s.role,
                instrument = s.instrument,
                percussionInstrument = s.percussionInstrument,
                pattern = s.pattern,
                style = inMem,
            };
        }

        /// <summary>
        /// D-SMOKE-MT-5=A / D-SMOKE-RT-3=A. Removes chunks that contain at
        /// least one NoteOn and whose NoteOns ALL sit on the metronome
        /// channel. Filtering by NOTE events (not any ChannelEvent)
        /// deliberately spares the conductor/meta chunk, which carries an
        /// AllSoundOff ControlChange on the metronome channel but no notes —
        /// a naive any-ChannelEvent filter would delete tempo and time
        /// signature.
        /// </summary>
        public static void StripMetronomeChunks(MidiFile file)
        {
            if (file == null)
                return;

            var toRemove = new List<TrackChunk>();
            foreach (var chunk in file.GetTrackChunks())
            {
                var noteOns = chunk.Events.OfType<NoteOnEvent>().ToList();
                if (noteOns.Count > 0 &&
                    noteOns.All(n => n.Channel == MidiGenerator.MetronomeChannel))
                    toRemove.Add(chunk);
            }
            foreach (var c in toRemove)
                file.Chunks.Remove(c);
        }

        /// <summary>
        /// Emits a deterministic per-render "fingerprint" so two renders (e.g.
        /// the editor window vs the runtime runner) can be diffed line-for-line
        /// to localize a byte-parity divergence. Both surfaces call this at the
        /// SAME point (post-render, pre-strip). Per chunk it reports note count,
        /// min/max octave (the register tell — a voicing-RNG divergence shows up
        /// as a shifted octave range on the backing/bass chunk) and a stable
        /// content hash over ordered (noteNumber, time, length, velocity) tuples.
        /// Read-only; mutates nothing.
        /// </summary>
        public static void LogRenderFingerprint(
            string surface, int baseSeed, SmokePartContext ctx, MidiFile file)
        {
            if (file == null)
                return;

            var sb = new StringBuilder();
            sb.Append($"[SmokeFingerprint:{surface}] seed={baseSeed} ");
            if (ctx != null)
                sb.Append($"tonality={ctx.tonality} root={ctx.rootNote} " +
                          $"ts={ctx.timeSignature} bars={ctx.measures} bpm={ctx.bpm}");
            sb.AppendLine();

            int idx = 0;
            foreach (var chunk in file.GetTrackChunks())
            {
                string name = chunk.Events.OfType<SequenceTrackNameEvent>()
                                  .FirstOrDefault()?.Text ?? $"chunk[{idx}]";
                var notes = chunk.GetNotes()
                    .OrderBy(n => n.Time).ThenBy(n => n.NoteNumber).ToList();

                if (notes.Count == 0)
                {
                    sb.AppendLine($"  #{idx} '{name}': notes=0 (meta/empty)");
                }
                else
                {
                    int minOct = notes.Min(n => n.NoteNumber) / 12 - 1;
                    int maxOct = notes.Max(n => n.NoteNumber) / 12 - 1;
                    uint h = 2166136261u; // FNV-1a
                    foreach (var n in notes)
                    {
                        unchecked
                        {
                            h = (h ^ (uint)(byte)n.NoteNumber) * 16777619u;
                            h = (h ^ (uint)n.Time) * 16777619u;
                            h = (h ^ (uint)n.Length) * 16777619u;
                            h = (h ^ (uint)(byte)n.Velocity) * 16777619u;
                        }
                    }
                    sb.AppendLine($"  #{idx} '{name}': notes={notes.Count} " +
                                  $"oct=[{minOct}..{maxOct}] hash={h:X8}");
                }
                idx++;
            }
            Debug.Log(sb.ToString().TrimEnd());
        }
    }
}