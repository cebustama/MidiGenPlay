using UnityEngine;
using MidiGenPlay.Composition.Phrases;
using Melanchall.DryWetMidi.Standards;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// Persisted generation-parameter bundle for the Melody Authoring Wizard
    /// (Roadmap_Melody_Authoring_MVP, Phase 1; accepted decisions #2 and #8).
    ///
    /// Saved independently of <see cref="MidiGenPlay.MelodyPatternData"/>: these params
    /// drive the Phase-3 simplified generator, which maps them into a pattern. The
    /// PATTERN is the persisted, runtime-consumed artifact; these params are a
    /// generation-time aid and are NOT read at runtime.
    ///
    /// Wraps optional references to the existing procedural-path assets so a designer
    /// can seed generation from an established personality / phrase vocabulary / style,
    /// plus the Tier-1 scalar knobs (decision #8).
    /// </summary>
    [CreateAssetMenu(menuName = "MidiGenPlay/Melody Generation Params")]
    public class MelodyGenerationParamsSO : ScriptableObject
    {
        [Header("Optional source assets (procedural-path references)")]
        [Tooltip("Optional. Personality / expression defaults to seed generation from.")]
        public MelodicLeadingConfig leadingConfig;

        [Tooltip("Optional. Phrase vocabulary to bias generated rhythm / contour.")]
        public PhrasePaletteSO phrasePalette;

        [Tooltip("Optional. Base strategy + per-phrase directives to seed pitch policy.")]
        public MelodicStyleSO melodicStyle;

        [Header("Tier-1 generation params (MVP scope)")]
        [Tooltip("Note density 0..1: sparser (0) to busier (1).")]
        [Range(0f, 1f)] public float density = 0.5f;

        [Tooltip("Lowest octave offset the generator may place notes at (relative to the pattern reference octave).")]
        public int octaveRangeMin = -1;

        [Tooltip("Highest octave offset the generator may place notes at (relative to the pattern reference octave).")]
        public int octaveRangeMax = 1;

        [Tooltip("Rhythmic feel for the simplified generator.")]
        public MelodyRhythmicStyle rhythmicStyle = MelodyRhythmicStyle.Even;

        [Tooltip("Scale/tonality hint for degree selection at generation time. The pattern " +
                 "stores scale degrees and stays tonality-agnostic; final pitch is resolved " +
                 "at runtime against the active Part tonality/root.")]
        public Tonality tonalityHint = Tonality.Ionian;

        [Tooltip("General MIDI melodic program hint for the wizard. INFORMATIONAL for the MVP: " +
                 "MelodyPatternData stores no instrument and the runtime instrument is chosen by " +
                 "the track config, so this does not change generated notes and is not read at " +
                 "runtime. Carried for display + future use.")]
        public GeneralMidiProgram instrumentHint = GeneralMidiProgram.AcousticGrandPiano;

        [Tooltip("Deterministic generation seed. Same seed + same params + same meter = same " +
                 "pattern. Re-roll for a different melody over the same rhythmic groove.")]
        public int seed = 0;

        /// <summary>Clamp scalar params into valid ranges and keep octave min &lt;= max.</summary>
        public void Normalize()
        {
            density = Mathf.Clamp01(density);
            if (octaveRangeMin > octaveRangeMax)
                (octaveRangeMin, octaveRangeMax) = (octaveRangeMax, octaveRangeMin);
        }
    }

    /// <summary>
    /// Rhythmic feel options for the MVP simplified melody generator (decision #8).
    /// Independent of the phrase archetypes (EvenFlow / BurstThenHold / SustainLeadIn);
    /// a future mapping from this enum to archetype selection is possible but out of MVP scope.
    /// </summary>
    public enum MelodyRhythmicStyle
    {
        Even,
        Syncopated,
        Burst
    }
}