using MidiGenPlay.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MidiGenPlay.Services
{
    public class SequenceSerializer : ISequenceSerializer
    {
        public string Serialize(IList<SongConfig.PartSequenceEntry> structure)
        {
            if (structure == null || structure.Count == 0) return string.Empty;

            var parts = new List<string>(structure.Count);
            foreach (var e in structure)
            {
                // store as 1-based for humans
                int oneBased = e.PartIndex + 1;
                if (e.RepeatCount > 1)
                    parts.Add($"{oneBased}x{e.RepeatCount}");
                else
                    parts.Add(oneBased.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(",", parts);
        }

        public bool TryParse(string raw, int partCount,
                             out List<SongConfig.PartSequenceEntry> structure,
                             out List<string> warnings)
        {
            structure = new List<SongConfig.PartSequenceEntry>();
            warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(raw))
            {
                warnings.Add("Sequence input is empty. Nothing to parse.");
                return false;
            }

            // tokens are comma-separated. each token supports "n" or "n x k"
            // accepted multipliers: 'x' or '×'
            var tokens = raw.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (t.Length == 0)
                {
                    warnings.Add($"Empty token at position {i} skipped.");
                    continue;
                }

                int repeat = 1;
                int number;

                // split by x or × (unicode times)
                int sep = IndexOfMultiplierSeparator(t);
                if (sep >= 0)
                {
                    var left = t.Substring(0, sep).Trim();
                    var right = t.Substring(sep + 1).Trim();

                    if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        warnings.Add(
                            $"Invalid sequence entry '{t}' at position {i} (left side). Skipped.");
                        continue;
                    }
                    if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out repeat) || repeat < 1)
                    {
                        warnings.Add($"Invalid repeat count in '{t}' at position {i}. Using 1.");
                        repeat = 1;
                    }
                }
                else
                {
                    if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        warnings.Add($"Invalid sequence entry '{t}' at position {i}. Skipped.");
                        continue;
                    }
                }

                // convert 1-based to 0-based index
                int idx = number - 1;

                // clamp into range
                if (partCount <= 0)
                {
                    warnings.Add("No parts exist. Sequence cannot reference any part.");
                    continue;
                }
                if (idx < 0 || idx >= partCount)
                {
                    int clampedNumber = Math.Clamp(number, 1, partCount);
                    warnings.Add(
                        $"Part number {number} (token {i}) is out of range. Using {clampedNumber}.");
                    idx = clampedNumber - 1;
                }

                structure.Add(new SongConfig.PartSequenceEntry
                {
                    PartIndex = idx,
                    RepeatCount = repeat
                });
            }

            return structure.Count > 0;
        }

        private static int IndexOfMultiplierSeparator(string token)
        {
            int ix = token.IndexOf('x');
            if (ix >= 0) return ix;
            return token.IndexOf('×'); // unicode times
        }
    }
}

