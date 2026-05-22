// MidiGenPlay.Runtime assembly metadata.
//
// MGP-ALWTTT-MOD-DIR-1.1: exposes internal members of MidiGenPlay.Runtime to the
// Editor-only test assembly MidiGenPlay.Tests.Editor. Used by tests that target
// internal seams (e.g. ChordTrackComposer.TryDirectionalFirstChordCore) where a
// full public-API fixture would be disproportionately heavy.
//
// Keep the InternalsVisibleTo list narrow — one entry per test assembly. Do not
// add production assemblies here; runtime-to-runtime visibility should stay on
// the public surface.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]