#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Standards;
using MidiGenPlay;
using UnityEditor;
using UnityEngine;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// One-shot editor utility that builds the seeded
    /// <c>Default Rhythm Genres.asset</c> — a fully-populated
    /// <see cref="RhythmGenreVocabularySO"/> carrying the eight v1 genres
    /// ported from R1's <c>genre_vocabulary.md</c>.
    /// </summary>
    /// <remarks>
    /// L2 deliverable per <c>Roadmap_LLM_Authoring_MVP.md</c>. The vocabulary
    /// asset is the genre source for the L2 Generate-button dropdown (D-L2.5).
    /// This builder constructs every <see cref="GenreEntry"/> in code so the
    /// asset can be regenerated deterministically rather than hand-entered in
    /// the Inspector.
    /// <para>
    /// Cue overrides port as prose in <see cref="SubStyleCue.guidance"/>
    /// (the SO has no structured per-cell override field in v1);
    /// <see cref="SubStyleCue.subdivisionsOverride"/> is set only where the
    /// source doc calls for a feel change (e.g. shuffle → triplets).
    /// </para>
    /// <para>
    /// Characteristic cells are stored as <b>one-bar anchors</b>
    /// (length == defaultBeatsPerMeasure × defaultSubdivisions). Genres whose
    /// source cells are canonically two bars (latin clave, drum'n'bass Amen)
    /// store the per-bar half here and capture the two-bar nature in
    /// <see cref="GenreEntry.styleDescriptors"/>.
    /// </para>
    /// </remarks>
    public static class RhythmGenreVocabularyBuilder
    {
        private const string TargetFolder =
            "Assets/Resources/ScriptableObjects/Vocabularies";
        private const string TargetPath =
            TargetFolder + "/Default Rhythm Genres.asset";

        [MenuItem("MidiGenPlay/Authoring/Create Default Rhythm Genres Asset")]
        public static void CreateDefaultAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<RhythmGenreVocabularySO>(TargetPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Default Rhythm Genres asset exists",
                    $"An asset already exists at:\n{TargetPath}\n\n" +
                    "Overwrite it with a freshly-built copy? " +
                    "Any manual edits to the existing asset will be lost.",
                    "Overwrite", "Cancel");
                if (!overwrite) return;
                AssetDatabase.DeleteAsset(TargetPath);
            }

            EnsureFolder(TargetFolder);

            var so = ScriptableObject.CreateInstance<RhythmGenreVocabularySO>();
            so.genres = BuildGenres();

            AssetDatabase.CreateAsset(so, TargetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(so);
            Selection.activeObject = so;
            Debug.Log($"[RhythmGenreVocabularyBuilder] Wrote {so.genres.Count} " +
                      $"genres to {TargetPath}.");
        }

        // -----------------------------
        // Folder helper
        // -----------------------------

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            // Create each missing segment under Assets/ in order.
            string[] parts = folder.Split('/');
            string accum = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = accum + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(accum, parts[i]);
                accum = next;
            }
        }

        // -----------------------------
        // Construction helpers
        // -----------------------------

        private static LaneSpec Lane(GeneralMidiPercussion instrument, int vel) =>
            new LaneSpec { instrument = instrument, defaultVelocity = vel };

        private static GlyphCell Cell(int laneIndex, string cell, string variant = "default") =>
            new GlyphCell { laneIndex = laneIndex, variant = variant, cell = cell };

        private static SubStyleCue Cue(string name, string guidance, int subdivisionsOverride = 0) =>
            new SubStyleCue
            {
                name = name,
                guidance = guidance,
                subdivisionsOverride = subdivisionsOverride
            };

        // -----------------------------
        // Genre table
        // -----------------------------

        private static List<GenreEntry> BuildGenres() => new List<GenreEntry>
        {
            BuildFunk(),
            BuildRock(),
            BuildJazz(),
            BuildHipHop(),
            BuildLatin(),
            BuildMetal(),
            BuildDrumAndBass(),
            BuildCountry(),
        };

        // ---- Funk (4/4, 2 bars, 16ths) ----
        private static GenreEntry BuildFunk() => new GenreEntry
        {
            genreName = "funk",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 4,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 100),
                Lane(GeneralMidiPercussion.AcousticSnare, 110),
                Lane(GeneralMidiPercussion.ClosedHiHat, 80),
                Lane(GeneralMidiPercussion.OpenHiHat, 90),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x..x..x...x....."), // kick — syncopated funk gesture
                Cell(1, "....x.......x..."), // snare — straight backbeat 2 & 4
                Cell(2, "xxxxxxxxxxxxxxxx"), // closed hat — steady 16ths
                Cell(3, "....X.......X..."), // open hat — on the backbeat
            },
            velocityConventions =
                "Snare backbeat at lane default (110); ghost notes use 'o' (50) — " +
                "the signature funk move. Kick stays at 'x'; accents rare. Hat steady " +
                "at 'x', 'X' only on a rare accented down.",
            styleDescriptors =
                "Pocket and syncopation. Ghost notes are the defining gesture — the " +
                "space between the backbeats is where funk lives. Don't fill every 16th " +
                "by default; leave room. Two bars: the second carries variation " +
                "(push, pull, or fill).",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("shuffle",
                    "Triplet feel — hat becomes a triplet pulse and snare ghosts move " +
                    "to triplet positions.", subdivisionsOverride: 3),
                Cue("JB-style",
                    "James Brown / Funky Drummer: dense ghost snares, 16th hat throughout, " +
                    "kick on 1 + 'ah of 1' + 'and of 2'. Aliases: James Brown, Funky Drummer."),
                Cue("P-funk",
                    "Parliament / Bootsy: wider kick spacing (often a one-drop on beat 3 " +
                    "only), sparse hat opens, snare can sit just on 3. Aliases: Parliament, Bootsy."),
            },
        };

        // ---- Rock (4/4, 2 bars, 8ths) ----
        private static GenreEntry BuildRock() => new GenreEntry
        {
            genreName = "rock",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 2,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 110),
                Lane(GeneralMidiPercussion.AcousticSnare, 115),
                Lane(GeneralMidiPercussion.ClosedHiHat, 85),
                Lane(GeneralMidiPercussion.OpenHiHat, 95),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x...x..."), // kick — 1 and 3
                Cell(1, "..x...x."), // snare — backbeat 2 and 4
                Cell(2, "xxxxxxxx"), // closed hat — steady 8ths
                Cell(3, "........"), // open hat — closed by default
            },
            velocityConventions =
                "Snare loud: 'x' at lane default (115); 'X' (120) reserved for the fill " +
                "or section-ending hit. Kick solid 'x', 'X' on big phrase-starting kicks. " +
                "Hat consistent — the point is steadiness, not dynamics.",
            styleDescriptors =
                "Backbeat-driven, square pocket by design. The point is the thud — rock " +
                "drums are less about syncopation than about hitting the right beats hard " +
                "at the right time.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("classic",
                    "AC/DC / straight rock: kick on 1 and 3, snare on 2 and 4, 8th hat. " +
                    "No ornamentation. Aliases: AC/DC, straight rock."),
                Cue("hard",
                    "Zeppelin / Bonham: kick syncopation in the back half of the bar; " +
                    "occasional 16th flourishes; ghost-snare OK but rare. Aliases: Zeppelin, Bonham."),
                Cue("half-time",
                    "Snare on 3 only; kick on 1; sparse. Heavy verses or stoner-rock feels."),
                Cue("punk",
                    "Kick on every 8th ('x.x.x.x.'); snare backbeat. Fast and stiff."),
            },
        };

        // ---- Jazz (4/4, 2 bars, triplets) ----
        private static GenreEntry BuildJazz() => new GenreEntry
        {
            genreName = "jazz",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 3,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 70),     // feathered kick
                Lane(GeneralMidiPercussion.AcousticSnare, 80), // light comping
                Lane(GeneralMidiPercussion.RideCymbal1, 90),   // time on the ride
                Lane(GeneralMidiPercussion.ClosedHiHat, 75),   // chick on 2 and 4
                Lane(GeneralMidiPercussion.OpenHiHat, 85),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x..x..x..x.."), // kick — feathered quarter pulse
                Cell(1, "....o.....o."), // snare — sparse ghost comp
                Cell(2, "x..x.xx.xx.x"), // ride — the spang-a-lang
                Cell(3, "...x.....x.."), // closed hat — chick on 2 and 4
                Cell(4, "............"), // open hat — silent by default
            },
            velocityConventions =
                "Ride is the loudest lane: 'x' at default (90), 'X' for accents on phrase " +
                "boundaries. Snare comp uses 'o' (50) ghost-volume, occasional 'x' for " +
                "emphasized comp hits. Hat chick on 2 and 4 is a solid 'x'. Kick feathered " +
                "at default (70); drop entirely for ballad feel.",
            styleDescriptors =
                "Time on the ride, not the snare. Comp; don't hammer. The triplet feel is " +
                "the foundation — straight 8ths break the spell. Less is almost always more.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("swing",
                    "Standard ride pattern + 2-and-4 hat chick + ghost snare comp. The default."),
                Cue("bebop",
                    "Uptempo; same ride pattern; more snare/kick 'bombs' (sudden loud " +
                    "accents, often on weak beats) — add 'X' strikes at unexpected positions."),
                Cue("ballad",
                    "Very sparse; mostly ride; brush-style snare comp; drop kick entirely."),
            },
        };

        // ---- Hip-hop (4/4, 2 bars, 16ths) ----
        private static GenreEntry BuildHipHop() => new GenreEntry
        {
            genreName = "hip-hop",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 4,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 105),
                Lane(GeneralMidiPercussion.AcousticSnare, 110),
                Lane(GeneralMidiPercussion.ClosedHiHat, 80),
                Lane(GeneralMidiPercussion.OpenHiHat, 90),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x..........x...."), // kick — boom bap: 1 and 'ah of 3'
                Cell(1, "....x.......x..."), // snare — backbeat 2 and 4
                Cell(2, "x.x.x.x.x.x.x.x."), // closed hat — 8ths (boom bap default)
                Cell(3, "..x...x...x...x."), // open hat — the 'and' pulse
            },
            velocityConventions =
                "Boom bap is laid back: mostly 'x', accents 'X' sparingly. Snare can be 'X' " +
                "for emphasis on phrase boundaries. Ghost snares 'o' are characteristic of " +
                "J Dilla-influenced boom bap.",
            styleDescriptors =
                "Pocket and backbeat. The 'ah of 3' kick is the gesture that separates " +
                "hip-hop from rock backbeat. The space between the kick and the backbeat is " +
                "where the rapper lives — leave room.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("boom bap",
                    "Kick on 1 and 'ah of 3'; backbeat snare; ghost snares between; 8th hat. " +
                    "Classic East Coast 90s."),
                Cue("trap",
                    "Double-time hats (16ths with bursts/rolls); syncopated skipping kick; " +
                    "snare often on 3 only (half-time backbeat); consider substituting Hand " +
                    "Clap alongside or instead of the snare and raising that lane's default " +
                    "velocity to ~115."),
                Cue("lo-fi",
                    "Sparse; kick mainly on 1; frequent 'o' ghost-snare comping; quiet hat " +
                    "(drop default velocity to ~65-70)."),
            },
        };

        // ---- Latin (4/4, 2 bars clave-bound, 16ths) ----
        private static GenreEntry BuildLatin() => new GenreEntry
        {
            genreName = "latin",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 4,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 90),
                Lane(GeneralMidiPercussion.AcousticSnare, 100),
                Lane(GeneralMidiPercussion.ClosedHiHat, 75),
                Lane(GeneralMidiPercussion.OpenHiHat, 85),
                Lane(GeneralMidiPercussion.SideStick, 95), // carries the clave
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x......x......x."), // kick — tumbao (per-bar)
                Cell(1, "..x.......x.x..."), // snare — light, on weak parts
                Cell(2, "x.x.xx.xx.x.xx.x"), // closed hat — cascara
                Cell(3, "................"), // open hat — silent by default
                Cell(4, "x.....x.....x..."), // side stick — 3-side of son clave (per-bar)
            },
            velocityConventions =
                "Side stick (clave) loud and present: 'x' at default (95), 'X' for accent " +
                "on rumba/guaguanco variants. Kick at default (90) — tumbao stays in the " +
                "pocket. Snare softer than the kick; use 'o' liberally for shell tones. " +
                "Cascara on hat steady at default (75).",
            styleDescriptors =
                "The clave is the spine. Every other lane carries tension and release " +
                "against the clave — don't write rhythms that disagree with it. The son " +
                "clave is fundamentally a TWO-BAR pattern (3-2 by default); the cells here " +
                "are per-bar anchors and should resolve over both bars.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("son", "3-2 son clave (default)."),
                Cue("tumbao",
                    "Emphasize the kick syncopation ('ah of 2' + 'and of 4'); clave in the " +
                    "background; cascara on the hat."),
                Cue("samba",
                    "Partido alto feel; surdo (kick) on beats 2 and 4; drop the side-stick " +
                    "lane and add a Surdo substitute (Low Tom or High Tom) if needed. 2/4 is " +
                    "more idiomatic but 4/4 works for v1."),
                Cue("bossa",
                    "Softer than samba; kick on 1 + 'and of 2'; side stick plays a 3-2 bossa " +
                    "clave (slightly different from son)."),
            },
        };

        // ---- Metal (4/4, 2 bars, 16ths) ----
        private static GenreEntry BuildMetal() => new GenreEntry
        {
            genreName = "metal",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 4,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 115),
                Lane(GeneralMidiPercussion.AcousticSnare, 120),
                Lane(GeneralMidiPercussion.ClosedHiHat, 90),
                Lane(GeneralMidiPercussion.OpenHiHat, 100),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x.xx.x.xx.x.xx.x"), // kick — Iron Maiden gallop
                Cell(1, "....X.......X..."), // snare — loud backbeat 2 and 4
                Cell(2, "xxxxxxxxxxxxxxxx"), // closed hat — 16ths driving
                Cell(3, "................"), // open hat — crashes at boundaries
            },
            velocityConventions =
                "Snare loud — but the lane default IS loud (120), so 'x' already hits hard; " +
                "'X' is the same value, reserved for chart consistency. Kick at default " +
                "(115); galloping figures don't accent further. Hat consistent.",
            styleDescriptors =
                "Aggression, density, precision. The kick pattern is the genre-defining " +
                "gesture; everything else follows the kick's lead. Metal grids are quantized " +
                "hard — don't express swing or shuffle here. Odd meters (7/8, 5/4, 9/8) are " +
                "common; respect a user meter override.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("blast",
                    "Blast beat: snare on every off-16th; kick on every 16th; 16th hat. " +
                    "Extreme density. Alias: blast beat."),
                Cue("gallop",
                    "Iron Maiden 8th-16th-16th kick pattern; standard backbeat snare; 8th hat."),
                Cue("double-time",
                    "Straight 16th kick; backbeat snare on 2 and 4; 16th hat. Driving and propulsive."),
                Cue("djent",
                    "Odd-meter override (7/8, 5/4); kick on the down + syncopated; snare on " +
                    "every 'and' position; 16th hat. Alias: polymeter."),
            },
        };

        // ---- Drum'n'bass (4/4, 2 bars, 16ths) ----
        private static GenreEntry BuildDrumAndBass() => new GenreEntry
        {
            genreName = "drum'n'bass",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 4,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 105),
                Lane(GeneralMidiPercussion.AcousticSnare, 115),
                Lane(GeneralMidiPercussion.ClosedHiHat, 85),
                Lane(GeneralMidiPercussion.OpenHiHat, 95),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x.......x......."), // kick — Amen-derived bar 1 (per-bar)
                Cell(1, "....x.......x.o."), // snare — backbeat + ghost at 'and of 4'
                Cell(2, "xxxxxxxxxxxxxxxx"), // closed hat — steady 16ths
                Cell(3, "....X.......X..."), // open hat — alongside backbeat snare
            },
            velocityConventions =
                "Snare LOUD — 'X' (120) liberally on the backbeat; it's the focal point. " +
                "Ghost snares 'o' (50) for the chopped-Amen feel between backbeats. Kick at " +
                "default (105). Hat steady; 16ths are the genre's pulse.",
            styleDescriptors =
                "Resolution is 16ths. Snare displacement (snare landing slightly late or " +
                "early, especially the 'ah of 4' hit) is the signature gesture — when the " +
                "snare lines up perfectly with the backbeat it stops sounding like DnB. " +
                "Canonically a TWO-BAR genre (Amen-derived); cells here are per-bar anchors " +
                "and the second bar should carry the displacement.",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("amen",
                    "Classic chopped Amen: backbeat snare with displacements; busy ghost " +
                    "snares; steady 16th hat. Densest."),
                Cue("liquid",
                    "Softer; fewer Amen-chops; cleaner backbeat snare on 2 and 4 only; hat " +
                    "can drop to 8ths in places."),
                Cue("neuro",
                    "Sparse; tight kick on 1 only per bar; snare on 2 and 4 with heavy 'X'; " +
                    "driving 16th hat."),
            },
        };

        // ---- Country (4/4, 2 bars, 8ths) ----
        private static GenreEntry BuildCountry() => new GenreEntry
        {
            genreName = "country",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 2,
            defaultSubdivisions = 2,
            defaultLaneComposition = new List<LaneSpec>
            {
                Lane(GeneralMidiPercussion.BassDrum1, 100),
                Lane(GeneralMidiPercussion.AcousticSnare, 105),
                Lane(GeneralMidiPercussion.ClosedHiHat, 80),
                Lane(GeneralMidiPercussion.OpenHiHat, 90),
            },
            characteristicCells = new List<GlyphCell>
            {
                Cell(0, "x...x..."), // kick — 1 and 3
                Cell(1, "..x...x."), // snare — backbeat 2 and 4
                Cell(2, "xxxxxxxx"), // closed hat — steady 8ths (restrained)
                Cell(3, "........"), // open hat — silent by default
            },
            velocityConventions =
                "Even velocity profile — country is about steady time, not dynamics. Most " +
                "steps at 'x' (lane default). 'X' (120) only for phrase-boundary emphasis, " +
                "rare. 'o' ghost notes uncommon; country snare is open and clean.",
            styleDescriptors =
                "Steady. The drums serve the song. Country drumming values consistency over " +
                "personality — when in doubt, simpler wins. Waltz country uses 3/4 " +
                "(respect a user meter override).",
            subStyleCues = new List<SubStyleCue>
            {
                Cue("train beat",
                    "Kick and snare alternate every 8th; hat steady. The most iconic country " +
                    "figure: kick 'x.x.x.x.', snare '.x.x.x.x'."),
                Cue("two-step",
                    "Standard backbeat (kick 'x...x...', snare '..x...x.'). Dancehall-friendly tempo."),
                Cue("shuffle",
                    "Triplet feel (Western swing): kick on beat-quarter triplets, snare on " +
                    "the 'trip-of-2' and 'trip-of-4' ghosts.", subdivisionsOverride: 3),
            },
        };
    }
}
#endif