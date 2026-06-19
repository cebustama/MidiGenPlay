#if UNITY_EDITOR
// Locks the DrumPatternPaletteSO.PickRandomPattern contract:
//   - weighted selection is deterministic under a seeded System.Random
//   - clone-on-pick returns an isolated instance (mutating it never touches the asset)
//   - empty / all-invalid entry lists return null rather than throwing
// Mirrors the chord palette's weighted-walk shape (D-PAL.4 = reuse weighted model).

using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using MidiGenPlay.Composition;
using NUnit.Framework;
using UnityEngine;

namespace MidiGenPlay.Tests.Editor
{
    public class DrumPatternPaletteSOTests
    {
        private static DrumPatternData MakePattern(string name, GeneralMidiPercussion instrument)
        {
            var p = ScriptableObject.CreateInstance<DrumPatternData>();
            p.name = name;
            p.beatsPerMeasure = 4;
            p.subdivisions = 1;
            p.Measures = 1;
            var lane = new DrumPatternData.Lane
            {
                instrument = instrument,
                defaultVelocity = 100,
                steps = new List<DrumPatternData.StepState>
                {
                    DrumPatternData.StepState.On(),
                    DrumPatternData.StepState.Off,
                    DrumPatternData.StepState.Off,
                    DrumPatternData.StepState.Off,
                }
            };
            p.lanes = new List<DrumPatternData.Lane> { lane };
            return p;
        }

        private static DrumPatternPaletteSO.WeightedEntry Entry(DrumPatternData pattern, float weight)
            => new DrumPatternPaletteSO.WeightedEntry { pattern = pattern, weight = weight };

        // ---------------------------------------------------------------------
        // Determinism
        // ---------------------------------------------------------------------

        [Test]
        public void PickRandomPattern_SameSeed_ProducesSameSequence()
        {
            var a = MakePattern("A", GeneralMidiPercussion.AcousticBassDrum);
            var b = MakePattern("B", GeneralMidiPercussion.AcousticSnare);
            var c = MakePattern("C", GeneralMidiPercussion.ClosedHiHat);
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry>
                {
                    Entry(a, 1f), Entry(b, 3f), Entry(c, 2f),
                };

                var seqA = new List<GeneralMidiPercussion>();
                var rngA = new System.Random(12345);
                for (int i = 0; i < 20; i++)
                {
                    var picked = palette.PickRandomPattern(rngA, cloneResult: false);
                    seqA.Add(picked.lanes[0].instrument);
                }

                var seqB = new List<GeneralMidiPercussion>();
                var rngB = new System.Random(12345);
                for (int i = 0; i < 20; i++)
                {
                    var picked = palette.PickRandomPattern(rngB, cloneResult: false);
                    seqB.Add(picked.lanes[0].instrument);
                }

                CollectionAssert.AreEqual(seqA, seqB,
                    "Same seed must yield identical pick sequences.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(a);
                ScriptableObject.DestroyImmediate(b);
                ScriptableObject.DestroyImmediate(c);
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        [Test]
        public void PickRandomPattern_RespectsWeights_ZeroWeightNeverPicked()
        {
            var picked = MakePattern("Picked", GeneralMidiPercussion.AcousticBassDrum);
            var never = MakePattern("Never", GeneralMidiPercussion.AcousticSnare);
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry>
                {
                    Entry(picked, 1f),
                    Entry(never, 0f), // zero weight => excluded as invalid
                };

                var rng = new System.Random(7);
                for (int i = 0; i < 50; i++)
                {
                    var result = palette.PickRandomPattern(rng, cloneResult: false);
                    Assert.AreEqual(GeneralMidiPercussion.AcousticBassDrum,
                        result.lanes[0].instrument,
                        "Zero-weight entries must never be selected.");
                }
            }
            finally
            {
                ScriptableObject.DestroyImmediate(picked);
                ScriptableObject.DestroyImmediate(never);
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        // ---------------------------------------------------------------------
        // Clone-on-pick isolation
        // ---------------------------------------------------------------------

        [Test]
        public void PickRandomPattern_CloneTrue_ReturnsIsolatedInstance()
        {
            var source = MakePattern("Source", GeneralMidiPercussion.AcousticBassDrum);
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry> { Entry(source, 1f) };

                var clone = palette.PickRandomPattern(new System.Random(1), cloneResult: true);

                Assert.AreNotSame(source, clone, "Clone must be a distinct instance.");

                // Mutate the clone; the source asset must be unaffected.
                clone.lanes[0].steps[1] = DrumPatternData.StepState.On();

                Assert.IsFalse(source.lanes[0].steps[1].active,
                    "Mutating the clone must not touch the source asset.");

                ScriptableObject.DestroyImmediate(clone);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(source);
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        [Test]
        public void PickRandomPattern_CloneFalse_ReturnsAssetReference()
        {
            var source = MakePattern("Source", GeneralMidiPercussion.AcousticBassDrum);
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry> { Entry(source, 1f) };

                var result = palette.PickRandomPattern(new System.Random(1), cloneResult: false);

                Assert.AreSame(source, result,
                    "cloneResult=false must return the original asset reference.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(source);
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        // ---------------------------------------------------------------------
        // Empty / invalid handling
        // ---------------------------------------------------------------------

        [Test]
        public void PickRandomPattern_NullEntries_ReturnsNull()
        {
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = null;
                Assert.IsNull(palette.PickRandomPattern(new System.Random(1)));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        [Test]
        public void PickRandomPattern_EmptyEntries_ReturnsNull()
        {
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry>();
                Assert.IsNull(palette.PickRandomPattern(new System.Random(1)));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        [Test]
        public void PickRandomPattern_AllInvalidEntries_ReturnsNull()
        {
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                // null pattern, null entry, and zero weight are all invalid.
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry>
                {
                    Entry(null, 1f),
                    null,
                    Entry(MakePattern("Zero", GeneralMidiPercussion.ClosedHiHat), 0f),
                };
                Assert.IsNull(palette.PickRandomPattern(new System.Random(1)));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        [Test]
        public void PickRandomPattern_NullRng_DoesNotThrow()
        {
            var source = MakePattern("Source", GeneralMidiPercussion.AcousticBassDrum);
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.entries = new List<DrumPatternPaletteSO.WeightedEntry> { Entry(source, 1f) };
                Assert.DoesNotThrow(() => palette.PickRandomPattern(null, cloneResult: false));
            }
            finally
            {
                ScriptableObject.DestroyImmediate(source);
                ScriptableObject.DestroyImmediate(palette);
            }
        }

        // ---------------------------------------------------------------------
        // Display name
        // ---------------------------------------------------------------------

        [Test]
        public void GetDisplayName_FallsBackToAssetName()
        {
            var palette = ScriptableObject.CreateInstance<DrumPatternPaletteSO>();
            try
            {
                palette.name = "MyPalette";
                palette.paletteDisplayName = "";
                Assert.AreEqual("MyPalette", palette.GetDisplayName());

                palette.paletteDisplayName = "Funk 4/4";
                Assert.AreEqual("Funk 4/4", palette.GetDisplayName());
            }
            finally
            {
                ScriptableObject.DestroyImmediate(palette);
            }
        }
    }
}
#endif