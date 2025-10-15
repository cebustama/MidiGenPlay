using UnityEngine;

namespace MidiGenPlay
{
    /// <summary>
    /// Minimal, data-only knobs composers/mutators can consult.
    /// All values are normalized or MIDI-note based, so they are easy to map.
    /// </summary>
    public interface IMusicianPersonality
    {
        string MusicianId { get; }

        /// <summary>How busy this musician tends to play [0..1]. 0=minimal, 1=very dense.</summary>
        float Density01 { get; }

        /// <summary>Preferred melodic/voicing range (MIDI note numbers, inclusive).</summary>
        int RangeLow { get; }
        int RangeHigh { get; }

        /// <summary>Scale/tonality bias (use enum ToString names to avoid coupling).</summary>
        string PreferredScaleId { get; } // e.g., "Ionian", "Dorian", "Aeolian"

        /// <summary>How ornamental this player is [0..1]. 0=plain, 1=grace notes, turns, fills.</summary>
        float Ornamentation01 { get; }

        /// <summary>Velocity bias [0..1]. 0=soft player, 1=aggressive.</summary>
        float VelocityBias01 { get; }
    }
}