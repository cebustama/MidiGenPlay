#if UNITY_EDITOR
using System.Collections.Generic;
using MidiGenPlay;
using MidiGenPlay.Composition;
using UnityEditor;
using UnityEngine;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// One-shot editor utility that builds the seeded
    /// <c>Default Chord Genres.asset</c> — a populated
    /// <see cref="ChordGenreVocabularySO"/> carrying a tight v1 set of
    /// chord-appropriate genres. Chord twin of
    /// <c>RhythmGenreVocabularyBuilder</c> (D-L4.8 = A).
    /// </summary>
    /// <remarks>
    /// <para>L4 deliverable. The vocabulary asset is the genre source for the
    /// chord LLM Generate panel; an empty asset makes every Generate fail with
    /// "genre not found", so this seeder is what makes the tool usable end to
    /// end. Built in code so the asset is regenerable deterministically rather
    /// than hand-entered in the Inspector.</para>
    ///
    /// <para><b>Self-check (load-bearing).</b> Every seeded characteristic
    /// progression is run through <see cref="RomanProgressionParser"/> AND the
    /// D-L4.5 token guard before the asset is written. A malformed or
    /// off-alphabet anchor aborts the build with a clear error, so the seed can
    /// never ship anchors that would themselves fail the zero-warning contract
    /// the prompt asks the model to honor.</para>
    ///
    /// <para><b>Anchors steer, they are not selected.</b> The progressions below
    /// are pasted into the prompt as examples; the LLM writes a fresh
    /// progression each run. Breadth of anchors per genre is the variation
    /// budget, so each genre carries several structurally distinct anchors
    /// rather than minor variants of one.</para>
    /// </remarks>
    public static class ChordGenreVocabularyBuilder
    {
        private const string TargetFolder =
            "Assets/Resources/ScriptableObjects/Vocabularies";
        private const string TargetPath =
            TargetFolder + "/Default Chord Genres.asset";

        [MenuItem("MidiGenPlay/Authoring/Create Default Chord Genres Asset")]
        public static void CreateDefaultAsset()
        {
            // ---- Build + self-check BEFORE any destructive file op ----
            var genres = BuildGenres();
            if (!SelfCheck(genres, out string selfCheckError))
            {
                EditorUtility.DisplayDialog(
                    "Chord vocabulary self-check failed",
                    "A seeded progression did not pass the parser/guard self-check, " +
                    "so the asset was NOT written:\n\n" + selfCheckError,
                    "OK");
                Debug.LogError($"[ChordGenreVocabularyBuilder] Self-check failed: {selfCheckError}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<ChordGenreVocabularySO>(TargetPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Default Chord Genres asset exists",
                    $"An asset already exists at:\n{TargetPath}\n\n" +
                    "Overwrite it with a freshly-built copy? " +
                    "Any manual edits to the existing asset will be lost.",
                    "Overwrite", "Cancel");
                if (!overwrite) return;
                AssetDatabase.DeleteAsset(TargetPath);
            }

            EnsureFolder(TargetFolder);

            var so = ScriptableObject.CreateInstance<ChordGenreVocabularySO>();
            so.genres = genres;

            AssetDatabase.CreateAsset(so, TargetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(so);
            Selection.activeObject = so;
            Debug.Log($"[ChordGenreVocabularyBuilder] Wrote {so.genres.Count} " +
                      $"genres to {TargetPath} (self-check passed).");
        }

        // -----------------------------
        // Self-check
        // -----------------------------

        /// <summary>
        /// Validate every characteristic progression in every genre against the
        /// parser and the D-L4.5 token guard. Returns false with a descriptive
        /// error on the first failure.
        /// </summary>
        private static bool SelfCheck(List<ChordGenreEntry> genres, out string error)
        {
            var parser = new RomanProgressionParser();
            foreach (var g in genres)
            {
                if (g?.characteristicProgressions == null) continue;
                foreach (var prog in g.characteristicProgressions)
                {
                    // Parser must accept it.
                    bool ok = parser.TryParse(
                        prog, g.defaultDurationMeasures,
                        inferTriadFromCaseWhenNoSuffix: true,
                        out _, out string parseError);
                    if (!ok)
                    {
                        error = $"[{g.genreName}] \"{prog}\" failed to parse: {parseError}";
                        return false;
                    }

                    // Guard must NOT flag it (no off-alphabet tokens).
                    if (ChordProgressionLLMResponseHandler.TryFindForbiddenToken(
                            prog, out string offending))
                    {
                        error = $"[{g.genreName}] \"{prog}\" contains off-alphabet token \"{offending}\".";
                        return false;
                    }
                }
            }
            error = null;
            return true;
        }

        // -----------------------------
        // Folder helper (mirrors drum builder)
        // -----------------------------

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
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

        private static ChordSubStyleCue Cue(string name, string guidance, int measuresOverride = 0) =>
            new ChordSubStyleCue
            {
                name = name,
                guidance = guidance,
                measuresOverride = measuresOverride
            };

        // -----------------------------
        // Genre table (tight v1 set: jazz, pop, blues, folk)
        // -----------------------------

        private static List<ChordGenreEntry> BuildGenres() => new List<ChordGenreEntry>
        {
            BuildJazz(),
            BuildPop(),
            BuildBlues(),
            BuildFolk(),
        };

        // ---- Jazz (4/4, 4 bars, 1 measure/chord) ----
        private static ChordGenreEntry BuildJazz() => new ChordGenreEntry
        {
            genreName = "jazz",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 4,
            defaultDurationMeasures = 1f,
            styleDescriptors =
                "Extended seventh-chord harmony; strong ii-V-I motion; secondary " +
                "dominants and chromatic approach chords; frequent modal interchange.",
            voicingHints =
                "Favour 7th chords throughout. Tonic = Imaj7, supertonic = ii7, " +
                "dominant = V7. Use ø7 for half-diminished ii in minor keys.",
            cadenceCues =
                "End on an authentic V7 - Imaj7. A ii7 - V7 setup before the tonic " +
                "is idiomatic. Optional tritone-sub flavour via bII7 before I.",
            characteristicProgressions = new List<string>
            {
                "ii7 – V7 – Imaj7 – vi7",      // classic turnaround
                "Imaj7 – vi7 – ii7 – V7",      // rhythm-changes A feel
                "iiø7 – V7 – i – i",           // minor ii-V-i
                "Imaj7 – V7 – vi7 – iii7",     // descending diatonic
                "bVII7 – Imaj7 – ii7 – V7",    // backdoor + turnaround
            },
            subStyleCues = new List<ChordSubStyleCue>
            {
                Cue("modal jazz",
                    "Static harmony; long dwell on a single modal centre. Use few " +
                    "chords, each held for two or more measures."),
                Cue("blues turnaround",
                    "12-bar jazz-blues skeleton with dominant 7ths on I, IV, V.",
                    measuresOverride: 12),
            },
        };

        // ---- Pop (4/4, 4 bars, 1 measure/chord) ----
        private static ChordGenreEntry BuildPop() => new ChordGenreEntry
        {
            genreName = "pop",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 4,
            defaultDurationMeasures = 1f,
            styleDescriptors =
                "Diatonic triad harmony; strong tonic/subdominant/dominant pull; " +
                "loop-friendly four-chord cycles; sparing use of sevenths.",
            voicingHints =
                "Mostly plain triads. Sevenths only as colour (e.g. a passing V7). " +
                "Major-key tonic feel; vi as the relative-minor anchor.",
            cadenceCues =
                "Loops often resolve I at the top of the next cycle rather than " +
                "with a strong cadence. IV - V - I gives a lift when needed.",
            characteristicProgressions = new List<string>
            {
                "I – V – vi – IV",     // axis of awesome
                "vi – IV – I – V",     // sensitive-female rotation
                "I – vi – IV – V",     // 50s doo-wop
                "IV – I – V – vi",     // displaced axis
                "I – IV – vi – V",     // pop-rock lift
            },
            subStyleCues = new List<ChordSubStyleCue>
            {
                Cue("doo-wop",
                    "Lean on the I - vi - IV - V rotation; held, even durations."),
                Cue("anthemic",
                    "Big IV - V - vi motion; suspended colour (Isus4 before I) welcome."),
            },
        };

        // ---- Blues (4/4, 12 bars, 1 measure/chord) ----
        private static ChordGenreEntry BuildBlues() => new ChordGenreEntry
        {
            genreName = "blues",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 12,
            defaultDurationMeasures = 1f,
            styleDescriptors =
                "Dominant-7th harmony on I, IV and V (the blues makes all three " +
                "dominant). 12-bar form is the backbone; quick-change and turnaround " +
                "variants common.",
            voicingHints =
                "I7, IV7, V7 throughout — dominant 7ths even on the tonic. " +
                "Optional #iv dim7 as a passing chord between IV7 and I7.",
            cadenceCues =
                "Bars 9-12 are the turnaround: V7 - IV7 - I7 - V7 sends the form " +
                "back to the top.",
            characteristicProgressions = new List<string>
            {
                // 12-bar, one chord per bar
                "I7 – I7 – I7 – I7 – IV7 – IV7 – I7 – I7 – V7 – IV7 – I7 – V7",
                // quick-change (IV7 in bar 2)
                "I7 – IV7 – I7 – I7 – IV7 – IV7 – I7 – I7 – V7 – IV7 – I7 – V7",
                // 8-bar blues
                "I7 – V7 – IV7 – IV7 – I7 – V7 – I7 – V7",
            },
            subStyleCues = new List<ChordSubStyleCue>
            {
                Cue("quick change",
                    "Move to IV7 in bar 2, back to I7 in bar 3.",
                    measuresOverride: 12),
                Cue("8-bar blues",
                    "Compressed 8-bar form instead of 12.",
                    measuresOverride: 8),
            },
        };

        // ---- Folk (4/4, 4 bars, 1 measure/chord) ----
        private static ChordGenreEntry BuildFolk() => new ChordGenreEntry
        {
            genreName = "folk",
            defaultMeter = TimeSignature.FourFour,
            defaultMeasures = 4,
            defaultDurationMeasures = 1f,
            styleDescriptors =
                "Open diatonic triad harmony; I-IV-V backbone with vi and ii for " +
                "colour; modal flavours (Mixolydian bVII) in traditional material.",
            voicingHints =
                "Plain triads; sus2/sus4 colour suits strummed guitar. The bVII " +
                "borrowed chord gives a modal, traditional feel.",
            cadenceCues =
                "Strong V - I or a plagal IV - I. Modal tunes may resolve bVII - I " +
                "instead of a dominant cadence.",
            characteristicProgressions = new List<string>
            {
                "I – IV – V – I",          // primary triads
                "I – V – IV – I",          // plagal-leaning
                "I – bVII – IV – I",       // Mixolydian / modal
                "vi – IV – I – V",         // minor-tinged folk-pop
                "I – IV – I – V",          // hymn-like
            },
            subStyleCues = new List<ChordSubStyleCue>
            {
                Cue("modal",
                    "Use the bVII borrowed chord; avoid the leading-tone V where a " +
                    "modal resolution fits."),
                Cue("waltz",
                    "Triple-meter feel; one chord per bar in 3/4. Set time signature " +
                    "to ThreeFour."),
            },
        };
    }
}
#endif