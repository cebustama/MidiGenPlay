using System.Collections.Generic;
using UnityEngine;

namespace MidiGenPlay.Interfaces
{
    public interface ISequenceSerializer
    {
        /// <summary>
        /// Convert entries to a concise string. 
        /// Example: 1,3,1 or 1x2,3,1x3 when RepeatCount > 1.
        /// </summary>
        string Serialize(IList<SongConfig.PartSequenceEntry> structure);

        /// <summary>
        /// Parse a free-form string (e.g., "1, 3, 1x2") into entries.
        /// - partCount is used to clamp 1-based numbers into range.
        /// - warnings contains human-readable issues (invalid tokens, clamping notices).
        /// Returns true if at least one valid entry was parsed.
        /// </summary>
        bool TryParse(string raw, int partCount,
                      out List<SongConfig.PartSequenceEntry> structure,
                      out List<string> warnings);
    }

}