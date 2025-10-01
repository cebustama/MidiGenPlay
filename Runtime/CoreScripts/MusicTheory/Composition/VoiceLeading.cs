using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.MusicTheory; // Note, NoteName
using UnityEngine;

namespace MidiGenPlay.Composition
{
    public interface IChordVoicer
    {
        IReadOnlyList<Note> VoiceChord(
            NoteName[] pitchClasses,
            MIDIInstrumentSO instrument,
            IReadOnlyList<Note> lastVoicing,
            VoiceLeadingConfig cfg);
    }

    // Minimal, fast, and tunable
    public sealed class BasicVoiceLeadingVoicer : IChordVoicer
    {
        public IReadOnlyList<Note> VoiceChord(
            NoteName[] pcs,
            MIDIInstrumentSO inst,
            IReadOnlyList<Note> last,
            VoiceLeadingConfig cfg)
        {
            // Candidate pitch-class sets (inversions & optional drop-2)
            var pcCandidates = GeneratePcCandidates(pcs, cfg);

            // Realize near target register and clamp to range
            int targetOct = TargetOctave(inst, last, cfg);

            var realizations = pcCandidates
                .Select(pc => RealizeNear(pc, targetOct, inst))
                .ToList();

            // Score & choose
            int bestIdx = 0; float bestScore = float.PositiveInfinity;
            for (int i = 0; i < realizations.Count; i++)
            {
                var cand = realizations[i];
                float score = Score(last, cand, cfg);
                if (score < bestScore) { bestScore = score; bestIdx = i; }
            }
            return realizations[bestIdx];
        }

        static IEnumerable<NoteName[]> GeneratePcCandidates(
            NoteName[] pcs, VoiceLeadingConfig cfg)
        {
            yield return pcs; // root
            if (!cfg.useInversions) yield break;

            // 1st, 2nd, 3rd inversion (if exists)
            for (int i = 1; i < pcs.Length && i < 4; i++)
                yield return Rotate(pcs, i);

            if (cfg.useDrop2 && pcs.Length >= 3)
            {
                // drop-2 on root position
                yield return Drop2(pcs);
            }
        }

        static NoteName[] Rotate(NoteName[] a, int k)
        {
            var b = new NoteName[a.Length];
            for (int i = 0; i < a.Length; i++) b[i] = a[(i + k) % a.Length];
            return b;
        }

        static NoteName[] Drop2(NoteName[] a)
        {
            // move the second-from-top down an octave (approx in pitch-class order)
            var b = (NoteName[])a.Clone();
            // for triad in root position: [R,3,5] -> drop 3rd below root
            (b[0], b[1], b[2]) = (b[1], b[0], b[2]); // simple swap to affect spacing after realization
            return b;
        }

        static int TargetOctave(
            MIDIInstrumentSO inst, IReadOnlyList<Note> last, VoiceLeadingConfig cfg)
        {
            // If we already have a voicing, steer near its average octave.
            if (last != null && last.Count > 0)
            {
                float avgOct = (float)last.Average(n => (double)n.Octave); // double -> float
                return Mathf.Clamp(Mathf.RoundToInt(avgOct), inst.octaveMin, inst.octaveMax);
            }

            // First chord: choose by mode.
            var center = (inst.octaveMin + inst.octaveMax) / 2;

            switch (cfg.startRegisterMode)
            {
                case VoiceLeadingConfig.StartRegisterMode.FixedOctave:
                    return Mathf.Clamp(
                        cfg.fixedStartingOctave, inst.octaveMin, inst.octaveMax);

                case VoiceLeadingConfig.StartRegisterMode.BiasFromCenter:
                    // convert semitone bias to octaves roughly (12 semis ~ 1 octave)
                    var biasOct = Mathf.RoundToInt(cfg.registerBiasSemitones / 12f);
                    return Mathf.Clamp(center + biasOct, inst.octaveMin, inst.octaveMax);

                default: // InstrumentCenter
                    return center;
            }
        }

        static IReadOnlyList<Note> RealizeNear(
            NoteName[] pcs, int nearOct, MIDIInstrumentSO inst)
        {
            var notes = new List<Note>(pcs.Length);
            int minOct = inst.octaveMin - 1, maxOct = inst.octaveMax - 1;

            // Start around nearOct and stack upwards, adjusting octaves monotonically
            int oct = nearOct;
            Note prev = null;
            foreach (var nn in pcs)
            {
                var n = Note.Get(nn, oct);
                if (prev != null && Semis(n) <= Semis(prev))
                {
                    while (Semis(n) <= Semis(prev)) n = Note.Get(nn, ++oct);
                }
                // clamp to range
                n = Note.Get(n.NoteName, Mathf.Clamp(n.Octave, minOct, maxOct));
                notes.Add(n);
                prev = n;
            }
            return notes;
        }

        public static int Semis(Note n)
        {
            // Melanchall.DryWetMidi.MusicTheory.Note exposes NoteNumber (SevenBitNumber).
            // Cast to byte then int to get 0..127.
            return (int)(byte)n.NoteNumber;
        }

        static float Score(
            IReadOnlyList<Note> last, IReadOnlyList<Note> next, VoiceLeadingConfig cfg)
        {
            float s = 0f;

            // 1) Movement (sum of absolute semitone distances)
            if (last != null && last.Count > 0)
            {
                int pairs = Mathf.Min(last.Count, next.Count);
                int movement = 0; int common = 0;
                for (int i = 0; i < pairs; i++)
                {
                    int a = Semis(last[i]);
                    int b = Semis(next[i]);
                    movement += Mathf.Abs(a - b);
                    if (a == b) common++;
                }
                s += cfg.weightMovement * movement;
                s -= cfg.weightCommonTone * common;
            }

            // 2) Spacing penalty (keep adjacent voices within [min,max])
            for (int i = 1; i < next.Count; i++)
            {
                int gap = Semis(next[i]) - Semis(next[i - 1]);
                if (gap < cfg.minTopInterval) s += cfg.weightSpacing * (cfg.minTopInterval - gap);
                if (gap > cfg.maxTopInterval) s += cfg.weightSpacing * (gap - cfg.maxTopInterval);
            }

            // 3) Register bias (soft pull toward the instrument center)
            // optional: could add another weight; we keep it light by nudging realization near target octave.

            return s;
        }
    }
}
