// Assets/MidiGenPlay/UI/ChordGridAdapterDefault.cs
using MidiGenPlay.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;   // for ScaleDegree, etc.

namespace MidiGenPlay.UI
{
    /// <summary>
    /// Maps ChordProgressionData ⇄ PatternGrid.
    /// - Builds a single-row grid sized by measures × beats × subdivisions.
    /// - Initial state lights cells where a chord starts.
    /// - WriteBack reads toggled cells and rebuilds the ChordEvent list,
    ///   preserving existing (degree, quality) pairs when possible.
    /// </summary>
    public sealed class ChordGridAdapter : IChordGridAdapter
    {
        private int _subdivisions = 1;
        public int Subdivisions => _subdivisions;

        public void BindToGrid(PatternGrid grid, ChordProgressionData data, int beatsPerMeasure, int measures)
        {
            if (!grid) return;

            // Keep adapter property in sync with the asset
            _subdivisions = Mathf.Max(1, data != null ? data.subdivisions : 1);

            // Derive anchors (true where a chord starts) from existing events
            bool[] anchors = null;
            if (data != null)
                anchors = data.BuildAnchorMask(beatsPerMeasure);

            // Prefer the caller-provided measures; fall back to the asset
            var m = measures > 0 ? measures : (data != null ? Mathf.Max(1, data.Measures) : 4);

            grid.Build(
                rows: 1,
                measures: m,
                beatsPerMeasure: Mathf.Max(1, beatsPerMeasure),
                subdivisions: _subdivisions,
                initialState: (r, s) => anchors != null && s < anchors.Length && anchors[s]
            );

            grid.SetFitToContent(width: true, height: true);    // fill vertically (1 row)
            grid.SetToggleReceivesClicks(false);                // don’t toggle on click
            grid.SetOverlayEnabled(false);                      // no green overlay
        }

        public void WriteBack(PatternGrid grid, ChordProgressionData data)
        {
            if (!grid || data == null) return;

            // Read anchor mask from grid
            var mask = new bool[grid.Steps];
            for (int s = 0; s < grid.Steps; s++)
                mask[s] = grid.GetCell(0, s);

            // Preserve existing (degree, quality) ordering where possible
            var ids = new List<(ScaleDegree, ChordQuality)>(data.events.Count);
            for (int i = 0; i < data.events.Count; i++)
                ids.Add((data.events[i].degree, data.events[i].quality));

            data.subdivisions = grid.Subdivisions; // keep asset synced with UI
            data.Measures = Mathf.Max(1, grid.Measures);

            // Rebuild the ChordEvent list from the new anchors
            data.RebuildFromAnchors(mask, ids);
        }
    }
}
