using System;
using System.Collections.Generic;
using UnityEngine;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay
{
    /// <summary>
    /// Genre knowledge for LLM-assisted chord-progression authoring.
    /// Data-driven so designers can extend without code changes.
    /// Consumed by <c>ChordProgressionLLMPromptBuilder</c> (editor-only);
    /// shipped as <c>Default Chord Genres.asset</c> in Resources.
    /// </summary>
    /// <remarks>
    /// L4 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c> (D-L4.2).
    /// Chord analogue of <see cref="RhythmGenreVocabularySO"/>: same
    /// genres[] + TryResolve + sub-style-cue shape, with drum-specific members
    /// (lane composition, glyph cells, velocity conventions) replaced by
    /// chord-domain members (characteristic progressions, voicing hints,
    /// cadence cues). The asset itself is purely data; runtime code does not
    /// consume it. Lives in Runtime/ for asset-system convenience and because
    /// <see cref="TimeSignature"/> is runtime-side; the chord progression
    /// strings it holds are plain Roman-numeral text consumed only by the
    /// editor-side prompt builder.
    /// </remarks>
    [CreateAssetMenu(menuName = "MidiGenPlay/Chord Genre Vocabulary")]
    public class ChordGenreVocabularySO : ScriptableObject
    {
        public List<ChordGenreEntry> genres = new List<ChordGenreEntry>();

        /// <summary>
        /// Resolve a user-supplied genre name or sub-style cue name to
        /// a <see cref="ChordGenreEntry"/> and an optional <see cref="ChordSubStyleCue"/>.
        /// Matches direct genre name first; then falls back to scanning
        /// every genre's <see cref="ChordGenreEntry.subStyleCues"/>.
        /// </summary>
        /// <param name="query">User-supplied name; matched case-insensitively.</param>
        /// <param name="genre">Resolved genre, or null if no match.</param>
        /// <param name="cue">Resolved sub-style cue, or null if the query matched a direct genre.</param>
        /// <returns>True if a genre was resolved.</returns>
        public bool TryResolve(string query, out ChordGenreEntry genre, out ChordSubStyleCue cue)
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
    /// A single genre in the chord vocabulary. Holds default mechanical
    /// parameters, characteristic progressions (Roman-string anchors),
    /// sub-style cues, and free-text guidance for the LLM.
    /// </summary>
    [Serializable]
    public class ChordGenreEntry
    {
        public string genreName;
        public TimeSignature defaultMeter = TimeSignature.FourFour;
        [Min(1)] public int defaultMeasures = 4;

        /// <summary>
        /// Default chord duration in measures when a generated chord omits its
        /// (x) suffix. Mirrors the editor's Default Duration field.
        /// </summary>
        [Min(0.001f)] public float defaultDurationMeasures = 1f;

        /// <summary>
        /// Characteristic progressions as Roman-numeral strings (anchors, not
        /// templates — the LLM varies within style). Each must itself be valid
        /// v1 DSL, e.g. "ii7 – V7 – Imaj7 – vi7". Structural twin of the drum
        /// SO's characteristicCells.
        /// </summary>
        public List<string> characteristicProgressions = new List<string>();

        public List<ChordSubStyleCue> subStyleCues = new List<ChordSubStyleCue>();

        /// <summary>
        /// Free-text quality/voicing conventions for the genre (e.g. "favour
        /// 7th chords; tonic is maj7, supertonic is m7"). Chord-domain twin of
        /// the drum SO's velocityConventions.
        /// </summary>
        [TextArea(2, 4)] public string voicingHints;

        /// <summary>
        /// Free-text cadence guidance (e.g. "end on authentic V7 – I; use
        /// half-cadence at the midpoint").
        /// </summary>
        [TextArea(2, 4)] public string cadenceCues;

        [TextArea(2, 4)] public string styleDescriptors;
    }

    /// <summary>
    /// A named variation of the parent genre (e.g., "modal jazz", "blues turnaround",
    /// "pop axis"). Unifies per-genre internal variations and cross-genre cues
    /// that route to this genre. Chord analogue of the drum <c>SubStyleCue</c>.
    /// </summary>
    [Serializable]
    public class ChordSubStyleCue
    {
        public string name;

        [TextArea(2, 4)] public string guidance;

        /// <summary>
        /// Mechanical override for total measures. 0 = no override.
        /// Example: a "blues turnaround" cue might set this to 12.
        /// </summary>
        [Min(0)] public int measuresOverride;
    }
}