using System;
using System.Collections.Generic;
using System.Linq;

namespace MidiGenPlay.Composition
{
    /// <summary>
    /// The shared "brain" of the composition smoke tooling (D-SMOKE-MT-3):
    /// turns a Part context + a list of track specs into a fully-formed
    /// <see cref="SongConfig"/> (one Part, one PartSequenceEntry, derived
    /// ChannelRoles / ChannelMusicianOrder), ready for the real render entry.
    ///
    /// Runtime-safe: no UnityEditor, no asset I/O, no ScriptableObject
    /// creation — it only wires references it is handed. Consumed by the
    /// editor CompositionSmokeWindow (Stage 1) and by the runtime
    /// CompositionSmokeRunner (Stage 2).
    ///
    /// Render-entry note (kept here because Stage 2 must repeat it):
    /// SongOrchestrator.GenerateSong IGNORES PartConfig.ExplicitBpm — it rolls
    /// a random BPM from Part.TempoRange via an unseeded RNG. Callers that
    /// want the context's BPM honored must render via
    /// <c>Orchestrator.GenerateSinglePart(song.Parts[0], song.ChannelRoles,
    /// partIndex: 0, bpmOverride: ctx.bpm, instrumentOverrides: null,
    /// seedOverride: seed)</c>, which performs the identical single-part
    /// assembly (meta chunk + metronome + PASS 1 / PASS 2).
    /// </summary>
    public static class SmokeSongConfigAssembler
    {
        /// <summary>
        /// Builds the single-part SongConfig. Throws ArgumentException on
        /// invalid input rather than degrading silently, so both consumers
        /// surface the same message.
        ///
        /// v1 constraint (verified against SongOrchestrator): track roles
        /// must be DISTINCT. The per-repetition producedByRole cache is keyed
        /// by role, so duplicate roles silently overwrite each other's output
        /// (and duplicate Rhythm tracks would additionally collide on GM
        /// channel 9 in BuildChannelMap). Deferred, not supported.
        /// </summary>
        public static SongConfig Assemble(
            SmokePartContext ctx,
            IReadOnlyList<SmokeTrackSpec> specs)
        {
            if (ctx == null)
                throw new ArgumentNullException(nameof(ctx));
            if (specs == null || specs.Count == 0)
                throw new ArgumentException(
                    "At least one track spec is required.", nameof(specs));

            // --- Validation: distinct roles (hard v1 constraint, see XML doc) ---
            var dupRoles = specs
                .Where(s => s != null)
                .GroupBy(s => s.role)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.ToString())
                .ToList();
            if (dupRoles.Count > 0)
                throw new ArgumentException(
                    "Duplicate roles are not supported in v1 (orchestrator " +
                    "caches tracks by role): " + string.Join(", ", dupRoles),
                    nameof(specs));

            // --- Validation: role-appropriate instrument present ---
            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                if (s == null)
                    throw new ArgumentException(
                        $"Track spec at index {i} is null.", nameof(specs));

                if (s.role == TrackRole.Rhythm)
                {
                    if (s.percussionInstrument == null)
                        throw new ArgumentException(
                            $"Track {i} ({s.role}): Rhythm requires a " +
                            "PercussionInstrument (RhythmTrackComposer reads " +
                            "cfg.PercussionInstrument).", nameof(specs));
                }
                else if (s.instrument == null)
                {
                    throw new ArgumentException(
                        $"Track {i} ({s.role}): a melodic Instrument is " +
                        "required for non-Rhythm roles.", nameof(specs));
                }
            }

            // --- Tracks + channel layout, index-aligned (D-SMOKE-MT-3) ---
            var tracks = new List<SongConfig.PartConfig.TrackConfig>(specs.Count);
            var channelRoles = new List<TrackRole>(specs.Count);
            var channelMusicians = new List<string>(specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                var s = specs[i];
                string musicianId = $"smoke_{s.role}_{i}";

                tracks.Add(new SongConfig.PartConfig.TrackConfig
                {
                    Role = s.role,
                    Instrument = s.instrument,
                    PercussionInstrument = s.percussionInstrument,
                    MusicianId = musicianId,
                    // Left at default (-1): the orchestrator assigns channels
                    // itself via BuildChannelMap (Rhythm -> 9, others 0..15
                    // skipping 9 and 15-is-metronome by count). TrackConfig
                    // .Channel is not read on the render path.
                    Channel = -1,
                    Parameters = new TrackParameters
                    {
                        Style = s.style,
                        // Fallback pattern/progression source. For harmony
                        // consumers (Bassline/Melody) the ctx lookup falls
                        // back to FindProgressionForPart, which scans these
                        // slots — so placing the SAME ChordProgressionData on
                        // the Backing row is sufficient and compose-order
                        // independent.
                        Pattern = s.pattern,
                    }
                });

                channelRoles.Add(s.role);
                channelMusicians.Add(musicianId);
            }

            var part = new SongConfig.PartConfig
            {
                Name = string.IsNullOrEmpty(ctx.partName) ? "SmokePart" : ctx.partName,
                Tracks = tracks,
                Tonality = ctx.tonality,
                RootNote = ctx.rootNote,
                TimeSignature = ctx.timeSignature,
                Measures = Math.Max(1, ctx.measures),
                Repetitions = 1,
                // Stamped for fidelity/inspection; NOT read by GenerateSong
                // (see class doc — render via GenerateSinglePart(bpmOverride)).
                ExplicitBpm = ctx.bpm,
                TempoScale = 1f,
            };

            return new SongConfig
            {
                Parts = new List<SongConfig.PartConfig> { part },
                Structure = new List<SongConfig.PartSequenceEntry>
                {
                    new SongConfig.PartSequenceEntry { PartIndex = 0, RepeatCount = 1 }
                },
                ChannelRoles = channelRoles,
                ChannelMusicianOrder = channelMusicians,
            };
        }
    }
}