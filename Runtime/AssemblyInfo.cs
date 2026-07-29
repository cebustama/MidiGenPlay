// MidiGenPlay.Runtime assembly metadata.
//
// MGP-ALWTTT-MOD-DIR-1.1: exposes internal members of MidiGenPlay.Runtime to the
// Editor-only test assembly MidiGenPlay.Tests.Editor.
//
// F-IVT-STALE (recorded and resolved at B0, 2026-07-26) - READ BEFORE RELYING ON THIS:
// this directive is currently INERT. No test in the package exercises internal
// access, and the two members this comment previously cited as "internal seams"
// are in fact public (ChordTrackComposer.TryDirectionalFirstChordCore,
// SongOrchestrator.ResolveTrackSeedPart) - as are BassTrackComposer.ResolveArticulation,
// SongOrchestrator.TrySeedDefaultProgression / DefaultProgressionSeedResult,
// SongOrchestrator.CreateSetRhythmOnsetsForPartMusician / CreateGetRhythmOnsetsForPart,
// and ChordProgressionRequality.TryMapCoreQuality. The likely cause is a mismatch
// between the name below and the real test .asmdef name.
//
// THE CONVENTION ON RECORD IS public: named seams that exist for EditMode tests are
// declared public static. See runtime/SSoT_Runtime_Generation_Orchestration.md 5.6.
// The entry is kept only as an escape hatch. A batch that wants the internal
// discipline back (F-IVT-STALE option b) must FIRST confirm the real test .asmdef
// name, then revert the seams and re-run the full suite; it is registered as a
// candidate on planning/active/Roadmap_Chord_Articulation.md.
//
// Keep the InternalsVisibleTo list narrow - one entry per test assembly. Do not
// add production assemblies here; runtime-to-runtime visibility should stay on
// the public surface.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]