using System;
using System.Collections.Generic;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// MGP-ALWTTT-DBG-1 (D-DBG1=A, converges with BASS-1): the composite key
    /// for every per-track surface of <see cref="PartRender"/> and for the
    /// per-render override map. A musicianId alone is NOT unique � the same
    /// musician can own several roles in one part � so all new surfaces are
    /// born keyed on (musicianId, TrackRole).
    /// </summary>
    public readonly struct MusicianTrackKey : IEquatable<MusicianTrackKey>
    {
        public readonly string MusicianId;
        public readonly TrackRole Role;

        public MusicianTrackKey(string musicianId, TrackRole role)
        {
            MusicianId = musicianId ?? string.Empty;
            Role = role;
        }

        public bool Equals(MusicianTrackKey other) =>
            Role == other.Role &&
            string.Equals(MusicianId, other.MusicianId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is MusicianTrackKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((MusicianId != null ? MusicianId.GetHashCode() : 0) * 397)
                       ^ (int)Role;
            }
        }

        public override string ToString() => $"{MusicianId}:{Role}";
    }

    /// <summary>
    /// MGP-ALWTTT-DBG-1 (Ask A): where a track's resolved pattern/progression
    /// actually came from, following each composer's documented precedence
    /// chain. <see cref="RenderOverride"/> is the Ask C per-render channel and
    /// always wins (precedence step 0, D-DBG4=A).
    /// </summary>
    public enum ResolvedSource
    {
        /// <summary>Nothing resolved / nothing rendered (e.g. missing inputs).</summary>
        None = 0,
        /// <summary>Ask C per-render override (precedence step 0).</summary>
        RenderOverride = 1,
        /// <summary>Card explicit override (patternOverride / progressionOverride).</summary>
        CardOverride = 2,
        /// <summary>Card palette pick (weighted, TS-aware).</summary>
        CardPalette = 3,
        /// <summary>Explicit TrackParameters.Pattern asset.</summary>
        TrackParameters = 4,
        /// <summary>The per-part shared progression cached in GenContext
        /// (bass always; backing/melody when another track built it first).</summary>
        SharedProgression = 5,
        /// <summary>Procedurally generated this render.</summary>
        Procedural = 6,
        /// <summary>MGP-ALWTTT-BASS-ORDER-1 (D-ORD-RB): the per-render SHARED
        /// progression was won by the HOST-supplied defaultProgression
        /// (MGP-ALWTTT-BASS-SOLO-1 channel). Stamped ONLY by the orchestrator
        /// into <c>PartRender.sharedProgressionSource</c> — composers never
        /// report it (a composer consuming the seeded default reports
        /// <see cref="SharedProgression"/>, which the orchestrator maps to
        /// this member using the seeding result). Appended member: values
        /// 0..6 are serialized/logged surface and unchanged.</summary>
        HostDefault = 7,
    }

    /// <summary>
    /// MGP-ALWTTT-DBG-1 (Ask A): the per-track readback payload � what one
    /// composer actually resolved for one render. Transported through
    /// <c>MidiGenerator.GenContext.ReportResolved</c> (D-DBG2=A), which
    /// <c>SongOrchestrator.GenerateOne</c> installs/collects with the same
    /// swap/restore discipline as <c>ctx.rng</c> / <c>ctx.trackSeed</c>.
    /// Identity is by source-asset NAME captured pre-clone (D-DBG3=A) � no
    /// GUIDs at runtime. <see cref="musicianId"/> and <see cref="role"/> are
    /// stamped authoritatively by the orchestrator's sink; composers only fill
    /// the content fields they know.
    /// Field population by role:
    ///  - Rhythm:  source, sourceAssetName, paletteName, proceduralStyleId.
    ///  - Backing: source, sourceAssetName, paletteName, progressionRoman,
    ///             resolvedFigures (Random articulation only, in emission order).
    ///  - Melody:  source, sourceAssetName (authored path);
    ///             melodyArchetypesBySpan (procedural path, one entry per chord span).
    ///  - Bassline: usesSharedProgression, source, progressionRoman.
    ///  - Harmony: NOT reported in v1 (out of the ALWTTT Asks; ID-2=A).
    /// </summary>
    public sealed class ResolvedTrackChoice
    {
        public string musicianId;
        public TrackRole role;

        public ResolvedSource source;

        /// <summary>Source asset name, captured PRE-clone (D-DBG3=A). Null for
        /// procedural output.</summary>
        public string sourceAssetName;

        /// <summary>Palette asset name when <see cref="source"/> ==
        /// <see cref="ResolvedSource.CardPalette"/>; null otherwise.</summary>
        public string paletteName;

        /// <summary>Rhythm procedural path only: the chosen IRhythmStyle id.</summary>
        public string proceduralStyleId;

        /// <summary>Backing/Bassline: compact roman-numeral sequence of the
        /// rendered progression (grid-site formatting, accidental-prefixed).</summary>
        public string progressionRoman;

        /// <summary>Backing under ChordExpressionType.Random: the resolved
        /// figure per chord event, in emission order (snapshot of
        /// RandomArticulationRoller.History). Null when articulation is fixed.</summary>
        public List<ChordExpressionType> resolvedFigures;

        /// <summary>Melody procedural path: the phrase-archetype asset name
        /// chosen for each chord span, in span order (null entry = palette had
        /// entries but every archetype reference was null).</summary>
        public List<string> melodyArchetypesBySpan;

        /// <summary>Bassline: true when the line was rendered from the
        /// per-part shared progression (GenContext cache) rather than an
        /// explicit TrackParameters asset.</summary>
        public bool usesSharedProgression;

        /// <summary>Backing, TONFILTER-1 (D-B2-2=B): true when the resolved
        /// progression's authored reference tonalities (descriptive metadata,
        /// D-B2-1=C — NOT a runtime filter) exclude the part's tonality AND
        /// the asset renders AsAuthored (no RUNTIME-REQUALITY adaptation).
        /// The render is intentional — the card's tonality wins — but the
        /// asset's authored qualities may read as modal borrowing; consider
        /// qualityRenderPolicy=DiatonicToPart on the asset. False whenever
        /// tonalities is empty, compatible, or requality is opted in.</summary>
        public bool tonalityMismatch;
    }

    /// <summary>
    /// MGP-ALWTTT-DBG-1 (D-DBG3=A): out-info for the card TS-aware pickers so
    /// the source identity is captured where the pre-clone asset is still in
    /// hand. Pure data; filling it changes no draw and no pick behavior.
    /// </summary>
    public struct PatternPickInfo
    {
        /// <summary>True when the pick came from the card's palette; false for
        /// the card's explicit override slot.</summary>
        public bool fromPalette;

        /// <summary>Pre-clone name of the picked source asset.</summary>
        public string sourceAssetName;

        /// <summary>Palette asset name when <see cref="fromPalette"/>.</summary>
        public string paletteName;
    }
}