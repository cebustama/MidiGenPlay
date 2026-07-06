using Melanchall.DryWetMidi.MusicTheory;
using MidiGenPlay.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay
{
    [System.Serializable]
    public class SongConfig
    {
        public List<PartConfig> Parts;
        public List<PartSequenceEntry> Structure;

        public List<string> ChannelMusicianOrder = new(); // channel index -> musicianId
        public List<TrackRole> ChannelRoles = new(); // channel index -> role (Rhythm/Lead/Backing)

        [System.Serializable]
        public class PartConfig
        {
            public string Name;
            public List<TrackConfig> Tracks;

            public Tonality Tonality;
            public NoteName RootNote;

            public TempoRange TempoRange;
            public int? ExplicitBpm;
            public float TempoScale = 1f;

            public TimeSignature TimeSignature;
            public int Measures;
            public int Repetitions;

            // ---- Transient, one-shot composer hints (NOT serialized, NOT persisted) ----
            // Written by upstream effects (e.g. ALWTTT ModulationEffect.apply()) just
            // before a render; consumed and cleared by composers. Not part of song state.
            //
            // See: MidiGenPlay.Composition.ModulationOctaveHint
            [System.NonSerialized]
            public Melanchall.DryWetMidi.MusicTheory.NoteName? PreviousRootNote;

            [System.NonSerialized]
            public MidiGenPlay.Composition.ModulationOctaveHint ModulationOctaveHint;

            // Per-chord inversion pin (CQ-A1-OBJ2), index-aligned to the rendered
            // progression's events. Entry semantics: null entry, a list shorter
            // than the event count, or no list at all => that chord is unset (the
            // voicer scores candidates freely); a value in 0..chordArity-1 => that
            // exact rotation is pinned (0 = root position — note: pinning 0 is NOT
            // the same as unset, it suppresses all other candidates); a value
            // outside 0..chordArity-1 => unset (safe no-op, never clamped; D2b=a).
            // Sticky-per-position (D2a=a): the pin applies at its event position on
            // EVERY pattern repeat within the render. Consumed and cleared by
            // ChordTrackComposer.Compose, so it applies to exactly one render. On
            // the render's very first chord the directional modulation hint above
            // wins when both are active (D3=A).
            //
            // See: runtime/SSoT_Composer_Backing_Track.md §7
            [System.NonSerialized]
            public IReadOnlyList<int?> ChordInversionHints;

            public override string ToString()
            {
                var ts = $"{TimeSignatureProperties[TimeSignature].BeatsPerMeasure}/" +
                         $"{TimeSignatureProperties[TimeSignature].BeatUnit}";
                var tracks = Tracks != null
                    ? string.Join(", ", Tracks.Select((t, i) => $"[{i}:{t}]"))
                    : "(no tracks)";
                return $"Part '{Name}' rep={Repetitions} TS={ts} Ton={Tonality} Root={RootNote} :: {tracks}";
            }

            /// <summary>
            /// A single track�s configuration
            /// </summary>
            [System.Serializable]
            public class TrackConfig
            {
                public MIDIInstrumentSO Instrument;
                public MIDIPercussionInstrumentSO PercussionInstrument;
                public TrackRole Role;
                public TrackParameters Parameters;

                public string MusicianId;
                public int Channel = -1;

                public override string ToString()
                {
                    var inst = Instrument != null ? Instrument.name
                             : PercussionInstrument != null ? PercussionInstrument.name
                             : "-";
                    var pat = Parameters?.Pattern != null ? Parameters.Pattern.name : "-";
                    var mus = !string.IsNullOrEmpty(MusicianId) ? MusicianId : "(unassigned)";
                    var ch = Channel >= 0 ? Channel.ToString() : "-";
                    return $"role={Role} mus={mus} ch={ch} inst={inst} pattern={pat}";
                }
            }
        }

        [System.Serializable]
        public class PartSequenceEntry
        {
            public int PartIndex;
            public int RepeatCount;
        }
    }

    #region Recipes
    [Serializable]
    public class RhythmRecipe
    {
        // explicit style id (e.g. set by a Rhythm card)
        public string RhythmStyleId;

        public enum HiHatDensity
        {
            From_Style,
            Quarter,
            Eighth
        }
        public HiHatDensity HatDensity = HiHatDensity.From_Style;

        public enum HatDensityMode
        {
            Fixed,           // same every bar
            AlternateByBar,  // flip each bar
            RandomPerBar
        }
        public HatDensityMode HatMode = HatDensityMode.Fixed;
    }

    [Serializable]
    public class BackingRecipe
    {
        public string BackingStyleId;
    }
    #endregion

    /// <summary>
    /// Base for any role-specific data (drum patterns, chord progressions, melodies�)
    /// </summary>
    [System.Serializable]
    public class TrackParameters
    {
        public PatternDataSO Pattern;

        public RhythmRecipe RhythmRecipe;
        public BackingRecipe BackingRecipe;

        public TrackStyleBundleSO Style;

        // === Legacy (still honored as a fallback; can deprecate later) ===
        public MelodicLeadingConfig melodicLeadingOverride;
        public MelodyStrategyId melodyStrategyId;

        public HarmonicLeadingConfig harmonicLeadingOverride;
        public HarmonyStrategyId harmonyStrategyId;
    }
}