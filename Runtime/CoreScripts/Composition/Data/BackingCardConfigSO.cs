using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    [CreateAssetMenu(menuName = "MidiGenPlay/TrackConfigs/BackingCardConfig")]
    public class BackingCardConfigSO : TrackStyleBundleSO
    {
        [Header("Voice Leading (optional override)")]
        public VoiceLeadingConfig voiceLeadingOverride;

        [Header("Chord Progression (optional card override)")]
        [Tooltip("If set, this progression will be used for the backing track " +
                 "instead of library/procedural generation.")]
        public ChordProgressionData progressionOverride;

        [Tooltip("Optional palette of candidate progressions for this card. " +
                 "If 'progressionOverride' is null, one will be picked at random " +
                 "from this palette using its internal weights.")]
        public ChordProgressionPaletteSO progressionPalette;

        /// <summary>
        /// Legacy picker (unchanged behavior):
        /// Priority:
        /// 1) progressionOverride (always wins if not null).
        /// 2) progressionPalette (weighted pick from palette asset).
        /// 3) null => no override; composer should fall back to library/procedural.
        ///
        /// Returns an instantiated (cloned) progression so runtime mutations never
        /// affect the asset in the project.
        /// </summary>
        public ChordProgressionData PickProgressionOverride(System.Random rng)
        {
            // 1) Single explicit override
            if (progressionOverride != null)
            {
                return ScriptableObject.Instantiate(progressionOverride);
            }

            // 2) Palette-based override
            if (progressionPalette != null)
            {
                var picked = progressionPalette.PickRandomProgression(rng, cloneResult: true);
                if (picked != null)
                    return picked;
            }

            // 3) No override defined
            return null;
        }

        /// <summary>
        /// TS-aware override picker (NEW):
        /// - If a card override exists, it still wins.
        /// - Else, tries to pick from progressionPalette using a two-step TS policy:
        ///   Tier A: exact TS match (optional, controlled by palette)
        ///   Tier B: ranked fallback heuristic
        ///   Tier C: raw weights if all heuristic scores collapse
        /// - Always returns a CLONE (never mutates assets).
        ///
        /// NOTE: This does not replace runtime TS adaptation; the composer will still
        /// normalize the chosen progression to the Part TS when necessary.
        /// </summary>
        public ChordProgressionData PickProgressionOverride(
            System.Random rng,
            TimeSignature desiredTimeSignature,
            MidiGenPlayConfig settings,
            bool verbose = false)
        {
            rng ??= new System.Random();

            // 1) Single explicit override always wins (composer will adapt TS if needed)
            if (progressionOverride != null)
            {
                if (verbose && progressionOverride.TimeSignature != desiredTimeSignature)
                {
                    Debug.Log($"[BackingCardConfigSO] progressionOverride TS={progressionOverride.TimeSignature} " +
                              $"does not match desired TS={desiredTimeSignature}. Will rely on runtime normalization.");
                }
                return ScriptableObject.Instantiate(progressionOverride);
            }

            // 2) Palette-based override (TS-aware if we can introspect candidates)
            if (progressionPalette != null)
            {
                if (TryExtractPaletteCandidates(progressionPalette, out var candidates) && candidates.Count > 0)
                {
                    bool preferExactTs = progressionPalette.preferExactTsMatches;

                    var picked = PickTwoStepTsAware(
                        candidates,
                        desiredTimeSignature,
                        rng,
                        settings,
                        preferExactTs,
                        verbose,
                        progressionPalette.GetDisplayName());

                    if (picked != null)
                        return ScriptableObject.Instantiate(picked);
                }

                // Fallback to palette's built-in weighted pick
                var fallback = progressionPalette.PickRandomProgression(rng, cloneResult: true);
                if (verbose && fallback != null)
                {
                    Debug.Log($"[BackingCardConfigSO] Palette TS-aware selection unavailable; " +
                              $"palette='{progressionPalette.GetDisplayName()}' preferExactTs={progressionPalette.preferExactTsMatches} " +
                              $"fallback pick='{fallback.DisplayName}' TS={fallback.TimeSignature} desiredTS={desiredTimeSignature}.");
                }
                return fallback;
            }

            return null;
        }

        // ---------------------------------------------------------------------
        // Internal: two-step selection
        // ---------------------------------------------------------------------

        private static ChordProgressionData PickTwoStepTsAware(
            List<(ChordProgressionData prog, float weight)> candidates,
            TimeSignature desiredTS,
            System.Random rng,
            MidiGenPlayConfig settings,
            bool preferExactTs,
            bool verbose,
            string paletteName = null)
        {
            // sanitize
            candidates = candidates
                .Where(c => c.prog != null)
                .Select(c => (c.prog, Mathf.Max(0.0001f, c.weight)))
                .ToList();

            if (candidates.Count == 0) return null;

            // Tier A: exact TS (optional)
            if (preferExactTs)
            {
                var exact = candidates.Where(c => c.prog.TimeSignature == desiredTS).ToList();
                if (exact.Count > 0)
                {
                    var picked = Roulette(exact, rng);
                    if (verbose && picked != null)
                    {
                        Debug.Log($"[BackingCardConfigSO] PROG_PICK source=palette tier=A(exactTS) " +
                                  $"preferExactTs=True palette='{paletteName}' " +
                                  $"pickedTS={picked.TimeSignature} desiredTS={desiredTS} picked='{picked.DisplayName}'.");
                    }
                    return picked;
                }
            }
            else if (verbose)
            {
                Debug.Log($"[BackingCardConfigSO] PROG_PICK source=palette tier=A skipped " +
                          $"preferExactTs=False palette='{paletteName}' desiredTS={desiredTS}.");
            }

            // Tier B: ranked fallback
            var scored = new List<(ChordProgressionData prog, float score)>(candidates.Count);
            foreach (var c in candidates)
            {
                float mult = ComputeTsHeuristicMultiplier(c.prog, desiredTS, settings);
                scored.Add((c.prog, c.weight * mult));
            }

            // If all scores collapsed, fall back to raw weights
            float total = scored.Sum(s => s.score);
            if (total <= 0f)
            {
                var picked = Roulette(candidates, rng);
                if (verbose && picked != null)
                {
                    Debug.Log($"[BackingCardConfigSO] PROG_PICK source=palette tier=C(rawWeights) " +
                              $"preferExactTs={preferExactTs} palette='{paletteName}' " +
                              $"pickedTS={picked.TimeSignature} desiredTS={desiredTS} picked='{picked.DisplayName}'.");
                }
                return picked;
            }

            var pickedB = Roulette(scored, rng);
            if (verbose && pickedB != null)
            {
                Debug.Log($"[BackingCardConfigSO] PROG_PICK source=palette tier=B(fallbackTS) " +
                          $"preferExactTs={preferExactTs} palette='{paletteName}' " +
                          $"pickedTS={pickedB.TimeSignature} desiredTS={desiredTS} picked='{pickedB.DisplayName}'.");
            }
            return pickedB;
        }

        private static float ComputeTsHeuristicMultiplier(
            ChordProgressionData prog,
            TimeSignature desiredTS,
            MidiGenPlayConfig settings)
        {
            // default to mild preference if props missing
            if (!TimeSignatureProperties.TryGetValue(prog.TimeSignature, out var src) ||
                !TimeSignatureProperties.TryGetValue(desiredTS, out var dst))
                return 1f;

            float srcBarQ = src.BeatsPerMeasure * (4f / src.BeatUnit);
            float dstBarQ = dst.BeatsPerMeasure * (4f / dst.BeatUnit);
            float barDiff = Mathf.Abs(srcBarQ - dstBarQ);

            float m = 1f;

            // B1) Bar-length equivalence (strong)
            if (barDiff < 0.001f) m *= 4.0f;
            else m *= 1f / (1f + barDiff);

            // B2) Same beat-unit (medium)
            if (src.BeatUnit == dst.BeatUnit) m *= 1.25f;

            // B3) parity (mild)
            if ((src.BeatsPerMeasure & 1) == (dst.BeatsPerMeasure & 1)) m *= 1.10f;

            // B4) numerator closeness (mild)
            m *= 1f / (1f + Mathf.Abs(src.BeatsPerMeasure - dst.BeatsPerMeasure) * 0.10f);

            // B5) subdivisions (mild)
            int minSub = settings != null ? Mathf.Max(1, settings.minHarmonicSubdivisions) : 4;
            int sub = Mathf.Max(1, prog.subdivisions);
            if (sub >= minSub) m *= 1.05f;
            else m *= 0.95f;

            // B6) chord density vs grouping count (mild, useful for 5/4 3+2 etc)
            int groupCount = DefaultGroupingCount(desiredTS);
            float startsPerBar = EstimateChordStartsPerBar(prog);
            m *= 1f / (1f + Mathf.Abs(startsPerBar - groupCount) * 0.25f);

            return m;
        }

        private static int DefaultGroupingCount(TimeSignature ts) => ts switch
        {
            TimeSignature.FourFour => 2,      // 2+2
            TimeSignature.ThreeFour => 1,     // [3]
            TimeSignature.TwoFour => 1,       // [2]
            TimeSignature.SixEight => 2,      // 3+3
            TimeSignature.NineEight => 3,     // 3+3+3
            TimeSignature.TwelveEight => 5,   // 3+3+2+2+2 (flamenco-ish)
            TimeSignature.FiveFour => 2,      // 3+2
            TimeSignature.SevenEight => 3,    // 2+2+3
            _ => 1
        };

        private static float EstimateChordStartsPerBar(ChordProgressionData prog)
        {
            int bars = Mathf.Max(1, prog.Measures);
            int starts = (prog.events != null) ? Mathf.Max(1, prog.events.Count) : 1;
            return starts / (float)bars;
        }

        // ---------------------------------------------------------------------
        // Internal: roulette helpers
        // ---------------------------------------------------------------------

        private static ChordProgressionData Roulette(
            List<(ChordProgressionData prog, float weight)> list,
            System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += Mathf.Max(0.0001f, list[i].weight);

            float pick = (float)rng.NextDouble() * total;
            for (int i = 0; i < list.Count; i++)
            {
                float w = Mathf.Max(0.0001f, list[i].weight);
                if (pick <= w)
                    return list[i].prog;
                pick -= w;
            }
            return list[list.Count - 1].prog;
        }

        // ---------------------------------------------------------------------
        // Internal: palette candidate extraction via reflection (best-effort)
        // ---------------------------------------------------------------------

        private static bool TryExtractPaletteCandidates(
            UnityEngine.Object palette,
            out List<(ChordProgressionData prog, float weight)> candidates)
        {
            candidates = new List<(ChordProgressionData prog, float weight)>();
            if (palette == null) return false;

            var t = palette.GetType();

            // 1) Look for fields/properties that are IEnumerable
            var members =
                t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 .Cast<MemberInfo>()
                 .Concat(t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

            foreach (var m in members)
            {
                object value = null;
                try
                {
                    value = m switch
                    {
                        FieldInfo fi => fi.GetValue(palette),
                        PropertyInfo pi when pi.GetIndexParameters().Length == 0 => pi.GetValue(palette),
                        _ => null
                    };
                }
                catch { /* ignore */ }

                if (value == null) continue;
                if (value is string) continue;

                if (value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item == null) continue;

                        if (item is ChordProgressionData direct)
                        {
                            candidates.Add((direct, 1f));
                            continue;
                        }

                        // Try common pattern: entry.progression + entry.weight
                        var it = item.GetType();

                        var prog = GetMemberValueClass<ChordProgressionData>(item, it, "progression")
                                   ?? GetMemberValueClass<ChordProgressionData>(item, it, "prog");
                        if (prog == null) continue;

                        float w =
                            GetMemberValueStruct<float>(item, it, "weight")
                            ?? GetMemberValueStruct<float>(item, it, "w")
                            ?? GetMemberValueStruct<float>(item, it, "probability")
                            ?? 1f;

                        candidates.Add((prog, Mathf.Max(0.0001f, w)));
                    }
                }
            }

            // De-dupe by reference, keep max weight
            if (candidates.Count > 0)
            {
                candidates = candidates
                    .GroupBy(c => c.prog)
                    .Select(g => (prog: g.Key, weight: g.Max(x => x.weight)))
                    .ToList();
                return true;
            }

            return false;
        }

        private static T? GetMemberValueStruct<T>(object obj, Type type, string name) where T : struct
        {
            try
            {
                var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(T))
                    return (T)f.GetValue(obj);

                var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(T) && p.GetIndexParameters().Length == 0)
                    return (T)p.GetValue(obj);
            }
            catch { }
            return null;
        }

        private static T GetMemberValueClass<T>(object obj, Type type, string name) where T : class
        {
            try
            {
                var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && typeof(T).IsAssignableFrom(f.FieldType))
                    return (T)f.GetValue(obj);

                var p = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && typeof(T).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
                    return (T)p.GetValue(obj);
            }
            catch { }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure it always shows up as Backing in the inspector.
            appliesTo = TrackRole.Backing;
        }
#endif
    }
}