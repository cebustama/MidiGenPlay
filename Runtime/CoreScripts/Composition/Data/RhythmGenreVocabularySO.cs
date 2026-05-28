using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using UnityEngine;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay
{
    /// <summary>
    /// Genre knowledge for LLM-assisted rhythm authoring.
    /// Data-driven so designers can extend without code changes.
    /// Consumed by <c>DrumPatternLLMPromptBuilder</c> (editor-only);
    /// shipped as <c>Default Rhythm Genres.asset</c> in Resources.
    /// </summary>
    /// <remarks>
    /// L1 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c> (D-L2 = A).
    /// The asset itself is purely data; runtime code does not consume it.
    /// Lives in Runtime/ for asset-system convenience and because the
    /// types it references (<see cref="TimeSignature"/>,
    /// <see cref="GeneralMidiPercussion"/>) are runtime-side.
    /// </remarks>
    [CreateAssetMenu(menuName = "MidiGenPlay/Rhythm Genre Vocabulary")]
    public class RhythmGenreVocabularySO : ScriptableObject
    {
        public List<GenreEntry> genres = new List<GenreEntry>();

        /// <summary>
        /// Resolve a user-supplied genre name or sub-style cue name to
        /// a <see cref="GenreEntry"/> and an optional <see cref="SubStyleCue"/>.
        /// Matches direct genre name first; then falls back to scanning
        /// every genre's <see cref="GenreEntry.subStyleCues"/>.
        /// </summary>
        /// <param name="query">User-supplied name; matched case-insensitively.</param>
        /// <param name="genre">Resolved genre, or null if no match.</param>
        /// <param name="cue">Resolved sub-style cue, or null if the query matched a direct genre.</param>
        /// <returns>True if a genre was resolved.</returns>
        public bool TryResolve(string query, out GenreEntry genre, out SubStyleCue cue)
        {
            genre = null;
            cue = null;
            if (string.IsNullOrWhiteSpace(query)) return false;
            string q = query.Trim().ToLowerInvariant();

            // Direct genre name match.
            foreach (var g in genres)
            {
                if (g == null) continue;
                if (string.Equals(g.genreName?.ToLowerInvariant(), q, StringComparison.Ordinal))
                {
                    genre = g;
                    return true;
                }
            }

            // Sub-style cue match.
            foreach (var g in genres)
            {
                if (g?.subStyleCues == null) continue;
                foreach (var c in g.subStyleCues)
                {
                    if (c == null) continue;
                    if (string.Equals(c.name?.ToLowerInvariant(), q, StringComparison.Ordinal))
                    {
                        genre = g;
                        cue = c;
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// A single genre in the vocabulary. Holds default mechanical parameters,
    /// default lane composition, characteristic cells per lane, sub-style cues,
    /// and free-text guidance for the LLM.
    /// </summary>
    [Serializable]
    public class GenreEntry
    {
        public string genreName;
        public TimeSignature defaultMeter = TimeSignature.FourFour;
        [Min(1)] public int defaultMeasures = 2;
        [Min(1)] public int defaultSubdivisions = 4;

        public List<LaneSpec> defaultLaneComposition = new List<LaneSpec>();
        public List<GlyphCell> characteristicCells = new List<GlyphCell>();
        public List<SubStyleCue> subStyleCues = new List<SubStyleCue>();

        [TextArea(2, 4)] public string velocityConventions;
        [TextArea(2, 4)] public string styleDescriptors;
    }

    /// <summary>
    /// One lane in a genre's default kit composition.
    /// </summary>
    [Serializable]
    public class LaneSpec
    {
        public GeneralMidiPercussion instrument = GeneralMidiPercussion.ClosedHiHat;
        [Range(1, 127)] public int defaultVelocity = 100;
    }

    /// <summary>
    /// A characteristic 1-bar (or 2-bar) glyph cell for a specific lane.
    /// Anchors, not templates — the LLM varies within style.
    /// </summary>
    [Serializable]
    public class GlyphCell
    {
        [Min(0)] public int laneIndex;
        public string variant = "default";
        public string cell;
    }

    /// <summary>
    /// A named variation of the parent genre (e.g., "JB-style", "boom bap",
    /// "shuffle"). Unifies per-genre internal variations and cross-genre
    /// cues that route to this genre.
    /// </summary>
    [Serializable]
    public class SubStyleCue
    {
        public string name;

        [TextArea(2, 4)] public string guidance;

        /// <summary>
        /// Mechanical override for subdivisions per beat. 0 = no override.
        /// Example: "shuffle" cue sets this to 3 to switch to triplet feel.
        /// </summary>
        [Min(0)] public int subdivisionsOverride;
    }
}