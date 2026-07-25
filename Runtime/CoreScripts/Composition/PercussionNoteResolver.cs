using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Standards;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// PERC-FALLBACK-1 — resolves a <see cref="GeneralMidiPercussion"/> request
    /// against a kit that may not map it exactly. Resolution order (D-PF2):
    /// (1) exact kit mapping; (2) first mapped family substitute in
    /// <see cref="PercussionFallbackTable"/> order; (3) if
    /// <paramref name="allowGmStandard"/>, the GM-standard note number
    /// (soundfont-dependent last resort); otherwise None (caller mutes the
    /// lane and warns).
    /// </summary>
    /// <remarks>
    /// Pure and deterministic: consumes the kit read-only via the existing
    /// <c>TryGetMappedNote</c>, walks a static fixed-order table, performs no
    /// logging and holds no state. Same kit + same input ⇒ same output.
    /// Log discipline (D-PF3) is the caller's responsibility — see
    /// <c>RhythmTrackComposer.TryResolveForCompose</c>.
    /// </remarks>
    public static class PercussionNoteResolver
    {
        /// <summary>How a percussion request was satisfied (or not).</summary>
        public enum Resolution
        {
            /// <summary>Kit maps the requested member directly.</summary>
            Exact,
            /// <summary>Kit maps a family substitute; see <c>resolvedAs</c>.</summary>
            Substituted,
            /// <summary>No family mapping; GM-standard note number emitted
            /// (only when <c>allowGmStandard</c> is true).</summary>
            GmStandard,
            /// <summary>Nothing playable; caller should mute and warn.</summary>
            None,
        }

        /// <summary>
        /// Tries to resolve <paramref name="percussion"/> to a playable note on
        /// <paramref name="kit"/>.
        /// </summary>
        /// <param name="kit">Active percussion kit; read-only. Null resolves to None.</param>
        /// <param name="percussion">Requested GM percussion member.</param>
        /// <param name="allowGmStandard">Opt-in last resort (D-PF6): emit the
        /// GM-standard note number when the kit maps nothing in the family.
        /// Only correct for GM-compliant soundfonts; wired false by the
        /// composer for now.</param>
        /// <param name="note">Resolved note; only meaningful when the method
        /// returns true.</param>
        /// <param name="resolution">How the request was satisfied.</param>
        /// <param name="resolvedAs">The member actually used: the request
        /// itself for Exact/GmStandard/None, the substitute for Substituted.</param>
        /// <returns>True when a playable note was produced
        /// (Exact / Substituted / GmStandard); false for None.</returns>
        public static bool TryResolve(
            MIDIPercussionInstrumentSO kit,
            GeneralMidiPercussion percussion,
            bool allowGmStandard,
            out Note note,
            out Resolution resolution,
            out GeneralMidiPercussion resolvedAs)
        {
            resolvedAs = percussion;

            if (kit != null)
            {
                // (1) Exact
                if (kit.TryGetMappedNote(percussion, out note))
                {
                    resolution = Resolution.Exact;
                    return true;
                }

                // (2) First mapped family substitute, fixed table order.
                var substitutes = PercussionFallbackTable.GetSubstitutes(percussion);
                for (int i = 0; i < substitutes.Count; i++)
                {
                    if (kit.TryGetMappedNote(substitutes[i], out note))
                    {
                        resolution = Resolution.Substituted;
                        resolvedAs = substitutes[i];
                        return true;
                    }
                }
            }

            // (3) GM-standard last resort (opt-in).
            if (allowGmStandard)
            {
                // DryWetMidi is the GM note-number authority (same seam rule
                // as DrumMidiImporter).
                note = Note.Get((SevenBitNumber)percussion.AsSevenBitNumber());
                resolution = Resolution.GmStandard;
                return true;
            }

            note = null;
            resolution = Resolution.None;
            return false;
        }
    }
}