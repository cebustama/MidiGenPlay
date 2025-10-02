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
                var bd = Score(last, cand, cfg);
                float score = bd.total;
                if (cfg.debugScoring)
                {
                    string gaps = (bd.gapsSemis == null || bd.gapsSemis.Length == 0)
                        ? "[]"
                        : "[" + string.Join(",", bd.gapsSemis) + "]";
                    string tag = bd.disqualified ? "DISQ" : "OK";

                    string lastStr = 
                        (last == null || last.Count == 0) ? "(none)" : DescribeVoicing(last);
                    Debug.Log(
                        $"[VL] cand#{i} {tag} " +
                        $"| last={lastStr} -> cand={DescribeVoicing(cand)} | " +
                        $"move={bd.movementSemis} | common={bd.commonExact} | gaps={gaps} | " +
                        $"shift={bd.shiftOctaves:0.00}oct | score={score:0.00}");
                }

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

                case VoiceLeadingConfig.StartRegisterMode.RandomAroundCenter:
                {
                    int maxDev = Mathf.Max(0, cfg.startRegisterRandomRangeSemitones);
                    int jitterSemis = UnityEngine.Random.Range(-maxDev, maxDev + 1);
                    int bias = Mathf.RoundToInt(jitterSemis / 12f);
                    return Mathf.Clamp(center + bias, inst.octaveMin, inst.octaveMax);
                }

                case VoiceLeadingConfig.StartRegisterMode.Uniform01AroundCenter:
                {
                    int min = inst.octaveMin;
                    int max = inst.octaveMax;

                    // half-range in octaves from center to either edge (integer)
                    int halfRange = Mathf.Max(0, Mathf.Max(center - min, max - center));

                    // max offset in octaves we allow (normalized by spread01)
                    int maxOffset = Mathf.RoundToInt(halfRange * Mathf.Clamp01(cfg.startRegisterSpread01));

                    // choose a side (down/up) uniformly, and an integer offset uniformly in [0..maxOffset]
                    int side = (UnityEngine.Random.value < 0.5f) ? -1 : 1;
                    int offset = UnityEngine.Random.Range(0, maxOffset + 1);

                    return Mathf.Clamp(center + side * offset, min, max);
                }

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

        struct ScoreBreakdown
        {
            public int movementSemis;          // sum |Δ| in semitones (paired voices)
            public int commonExact;            // exact matches (name+octave)
            public int[] gapsSemis;            // adjacent voice intervals (semitones)
            public float spacingPenalty;       // penalty from gaps out of band
            public float shiftOctaves;         // |avg(next) - avg(last)|
            public float shiftExcessPenalty;   // penalty for excess over cfg.maxOctaveShiftPerChord
            public float total;                // total score
            public bool disqualified;         // true when hardLimitOctaveShift
        }

        static ScoreBreakdown Score(IReadOnlyList<Note> last, IReadOnlyList<Note> next, VoiceLeadingConfig cfg)
        {
            var sb = new ScoreBreakdown();
            float s = 0f;

            // 1) Movement / Common tones
            if (last != null && last.Count > 0)
            {
                int pairs = Mathf.Min(last.Count, next.Count);
                for (int i = 0; i < pairs; i++)
                {
                    int a = Semis(last[i]);
                    int b = Semis(next[i]);
                    sb.movementSemis += Mathf.Abs(a - b);
                    if (a == b) sb.commonExact++;
                }
                s += cfg.weightMovement * sb.movementSemis;
                s -= cfg.weightCommonTone * sb.commonExact;
            }

            // 2) Spacing penalties
            sb.gapsSemis = new int[Mathf.Max(0, next.Count - 1)];
            for (int i = 1; i < next.Count; i++)
            {
                int gap = Semis(next[i]) - Semis(next[i - 1]);
                sb.gapsSemis[i - 1] = gap;
                if (gap < cfg.minTopInterval) s += cfg.weightSpacing * (cfg.minTopInterval - gap);
                if (gap > cfg.maxTopInterval) s += cfg.weightSpacing * (gap - cfg.maxTopInterval);
            }
            sb.spacingPenalty = s;

            // 3) Register drift (avg octave)
            if (last != null && last.Count > 0)
            {
                float lastAvg = (float)last.Average(n => (double)n.Octave);
                float nextAvg = (float)next.Average(n => (double)n.Octave);
                sb.shiftOctaves = Mathf.Abs(nextAvg - lastAvg);

                float excess = Mathf.Max(0f, sb.shiftOctaves - cfg.maxOctaveShiftPerChord);
                if (cfg.hardLimitOctaveShift && excess > 0f)
                {
                    sb.disqualified = true;
                    sb.total = float.PositiveInfinity;
                    return sb;
                }

                if (excess > 0f)
                    s += cfg.weightShiftExcess * (excess * 12f); // scale like semitones
                sb.shiftExcessPenalty = s - sb.spacingPenalty - (cfg.weightMovement * sb.movementSemis - cfg.weightCommonTone * sb.commonExact);
            }

            sb.total = s;
            return sb;
        }

        static string DescribeVoicing(IReadOnlyList<Note> v)
            => string.Join("-", v.Select(n => $"{n.NoteName}{n.Octave}"));
    }
}
