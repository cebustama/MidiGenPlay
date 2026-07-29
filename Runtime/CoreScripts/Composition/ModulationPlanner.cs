using System.Collections.Generic;
using System.Linq;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// MOD-1 (HARMONY-PURE-1) — pure modulation planning primitive.
    ///
    /// Given a source key (tonic pitch class + mode) and a target key, this
    /// computes the raw musical material a host needs to stage a modulation:
    ///   1. the FUNCTIONAL DOMINANT of the target (root a perfect 5th above
    ///      the target tonic, quality Dominant7 — expressed both as a pitch
    ///      class and as (Dominant degree, accidental) relative to the
    ///      target mode, so it drops straight into ChordProgressionData
    ///      events);
    ///   2. PIVOT CHORD candidates — the intersection of the two keys'
    ///      diatonic triads (matched by root pitch class + triad quality),
    ///      RANKED so candidates with SUBDOMINANT function in the TARGET
    ///      (ii / IV) come first (the common-practice pivot placement:
    ///      pivot-as-pre-dominant, then the target's dominant, then its
    ///      tonic), with a SEEDED deterministic tiebreak inside each rank
    ///      band (D-MOD-OUT=A);
    ///   3. COMMON TONES — the pitch-class intersection of the two scales.
    ///
    /// The package deliberately returns a PLAN, not a progression: timing,
    /// durations and placement of the modulation are game decisions. The
    /// host consumes this through the existing patternOverride surface —
    /// zero composer edits (HARMONY-PURE-1 invariant).
    ///
    /// Determinism: pure function of its arguments. Zero rng draws — the
    /// seed only orders ties via an FNV-1a hash (no UnityEngine.Random, no
    /// System.Random, no stream perturbation anywhere).
    /// </summary>
    public static class ModulationPlanner
    {
        /// <summary>
        /// One pivot candidate: a triad diatonic to BOTH keys.
        /// </summary>
        public struct PivotCandidate
        {
            /// <summary>Root pitch class (0..11, 0 = C).</summary>
            public int rootPitchClass;

            /// <summary>Shared triad quality (Major or Minor or Diminished/Augmented).</summary>
            public ChordQuality quality;

            /// <summary>Degree this triad occupies in the SOURCE key.</summary>
            public ScaleDegree degreeInSource;

            /// <summary>Degree this triad occupies in the TARGET key.</summary>
            public ScaleDegree degreeInTarget;

            /// <summary>
            /// True when the triad carries subdominant function in the
            /// target (degreeInTarget is Supertonic or Subdominant) — the
            /// ranking's first band.
            /// </summary>
            public bool subdominantInTarget;
        }

        /// <summary>
        /// The full plan. Lists are freshly allocated per call — callers may
        /// take ownership.
        /// </summary>
        public struct ModulationPlan
        {
            /// <summary>Root pitch class of the target's functional dominant.</summary>
            public int dominantRootPitchClass;

            /// <summary>Always Dominant7 (functional cadence practice).</summary>
            public ChordQuality dominantQuality;

            /// <summary>
            /// The dominant expressed against the TARGET mode's scale:
            /// degree is always ScaleDegree.Dominant; the accidental is 0 in
            /// every diatonic mode except Locrian (+1, raising the b5 to a
            /// perfect 5th). Ready for a ChordProgressionData event.
            /// </summary>
            public ScaleDegree dominantDegreeInTarget;
            public int dominantAccidentalInTarget;

            /// <summary>Pivot candidates, ranked (see class summary).</summary>
            public List<PivotCandidate> pivots;

            /// <summary>Common pitch classes of the two scales, ascending.</summary>
            public List<int> commonTonePitchClasses;
        }

        /// <summary>
        /// Convenience overload taking DryWetMidi note names for the tonics.
        /// </summary>
        public static ModulationPlan Plan(
            Melanchall.DryWetMidi.MusicTheory.NoteName sourceTonic,
            Tonality sourceMode,
            Melanchall.DryWetMidi.MusicTheory.NoteName targetTonic,
            Tonality targetMode,
            int seed)
            => Plan((int)sourceTonic, sourceMode,
                    (int)targetTonic, targetMode, seed);

        /// <summary>
        /// Core entry. Tonics are pitch classes 0..11 (0 = C); values outside
        /// are wrapped. Pure and deterministic — same arguments, same plan,
        /// down to list order.
        /// </summary>
        public static ModulationPlan Plan(
            int sourceTonicPc, Tonality sourceMode,
            int targetTonicPc, Tonality targetMode,
            int seed)
        {
            sourceTonicPc = Wrap12(sourceTonicPc);
            targetTonicPc = Wrap12(targetTonicPc);

            int[] srcOffsets = GetDegreeSemitoneOffsets(sourceMode);
            int[] tgtOffsets = GetDegreeSemitoneOffsets(targetMode);

            // ---- 1) Functional dominant of the target -------------------
            int dominantPc = Wrap12(targetTonicPc + 7);
            // Accidental of "tonic + 7 semitones" against the target mode's
            // own Dominant degree (offset 7 everywhere except Locrian's 6).
            int dominantAcc = 7 - tgtOffsets[(int)ScaleDegree.Dominant];

            // ---- 2) Pivot candidates ------------------------------------
            var pivots = new List<PivotCandidate>();
            for (int sd = 0; sd < 7; sd++)
            {
                int srcRoot = Wrap12(sourceTonicPc + srcOffsets[sd]);
                var srcQ = GetDiatonicTriadQuality(sourceMode, (ScaleDegree)sd);

                for (int td = 0; td < 7; td++)
                {
                    int tgtRoot = Wrap12(targetTonicPc + tgtOffsets[td]);
                    if (tgtRoot != srcRoot)
                        continue;
                    var tgtQ = GetDiatonicTriadQuality(
                        targetMode, (ScaleDegree)td);
                    if (tgtQ != srcQ)
                        continue;

                    var degInTarget = (ScaleDegree)td;
                    pivots.Add(new PivotCandidate
                    {
                        rootPitchClass = srcRoot,
                        quality = srcQ,
                        degreeInSource = (ScaleDegree)sd,
                        degreeInTarget = degInTarget,
                        subdominantInTarget =
                            degInTarget == ScaleDegree.Supertonic ||
                            degInTarget == ScaleDegree.Subdominant,
                    });
                }
            }

            // Rank: subdominant-in-target first; seeded FNV tiebreak inside
            // each band (D-MOD-OUT=A). OrderBy is a stable sort, but the
            // hash makes the in-band order an explicit function of the seed
            // rather than of discovery order.
            pivots = pivots
                .OrderBy(p => p.subdominantInTarget ? 0 : 1)
                .ThenBy(p => TieHash(seed, p.rootPitchClass,
                                     (int)p.degreeInTarget))
                .ThenBy(p => (int)p.degreeInTarget) // total order safety net
                .ToList();

            // ---- 3) Common tones ----------------------------------------
            var srcScale = new HashSet<int>(
                srcOffsets.Select(o => Wrap12(sourceTonicPc + o)));
            var common = tgtOffsets
                .Select(o => Wrap12(targetTonicPc + o))
                .Where(srcScale.Contains)
                .Distinct()
                .OrderBy(pc => pc)
                .ToList();

            return new ModulationPlan
            {
                dominantRootPitchClass = dominantPc,
                dominantQuality = ChordQuality.Dominant7,
                dominantDegreeInTarget = ScaleDegree.Dominant,
                dominantAccidentalInTarget = dominantAcc,
                pivots = pivots,
                commonTonePitchClasses = common,
            };
        }

        private static int Wrap12(int pc) => ((pc % 12) + 12) % 12;

        /// <summary>
        /// FNV-1a over (seed, rootPc, degree). Deterministic across
        /// platforms and runtimes — never use string.GetHashCode for
        /// anything that must reproduce.
        /// </summary>
        public static uint TieHash(int seed, int rootPc, int degreeInTarget)
        {
            unchecked
            {
                const uint fnvPrime = 16777619u;
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * fnvPrime;
                h = (h ^ (uint)rootPc) * fnvPrime;
                h = (h ^ (uint)degreeInTarget) * fnvPrime;
                return h;
            }
        }
    }
}