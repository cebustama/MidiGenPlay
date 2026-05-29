// MidiGenPlay.Editor assembly metadata.
//
// Exposes internal members of MidiGenPlay.Editor to the EditMode test assembly
// MidiGenPlay.Tests.Editor. Added in Batch L4 (D-L4.6) so the chord LLM guard
// helper ChordProgressionLLMResponseHandler.TryFindForbiddenToken can be unit-
// tested directly rather than only through the public FromPayload path. Mirrors
// the Runtime assembly's existing InternalsVisibleTo convention
// (MGP-ALWTTT-MOD-DIR-1.1).
//
// Also retroactively reaches other editor-side internals previously testable
// only through their public entry points (e.g. DrumPatternEditorImporter
// .ExtractDslLines).
//
// Keep the InternalsVisibleTo list narrow — one entry per test assembly. Do not
// add production assemblies here.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MidiGenPlay.Tests.Editor")]