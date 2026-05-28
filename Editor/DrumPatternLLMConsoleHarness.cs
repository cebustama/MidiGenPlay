#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Standards;
using UnityEditor;
using UnityEngine;
using BCS.LLM.Core.Clients;
using TimeSignature = MidiGenPlay.MusicTheory.MusicTheory.TimeSignature;

namespace MidiGenPlay.Authoring
{
    /// <summary>
    /// One-shot console harness for end-to-end LLM drum pattern generation.
    /// L1 DoD bullet 3 ("LLM Core call succeeds end-to-end in a one-shot
    /// console harness") and source of empirical token counts for tightening
    /// D-L4 cost guardrails.
    /// </summary>
    /// <remarks>
    /// Usage:
    /// <list type="number">
    ///   <item><description>In the Unity Project window, select an <c>LLMClientData</c> asset (e.g. an AnthropicClientData config).</description></item>
    ///   <item><description>Invoke <c>MidiGenPlay → Tools → Test LLM Drum Generation</c>.</description></item>
    ///   <item><description>Read results in the Unity Console once the LLM responds.</description></item>
    /// </list>
    /// <para>
    /// The handler is <c>async void</c>: the editor stays responsive while the
    /// HTTP call is in flight. Do NOT use <c>.GetAwaiter().GetResult()</c> here —
    /// the awaited continuation must return to Unity's main thread, and blocking
    /// that thread deadlocks the editor.
    /// </para>
    /// <para>
    /// The harness constructs a single-genre in-memory vocabulary (funk only,
    /// matching the test fixture) so it does not depend on the
    /// <c>Default Rhythm Genres.asset</c> existing.
    /// </para>
    /// </remarks>
    public static class DrumPatternLLMConsoleHarness
    {
        private const string LogTag = "[LLM Harness]";

        [MenuItem("MidiGenPlay/Tools/Test LLM Drum Generation")]
        public static async void RunHarness()
        {
            // ---- 1. Validate selection ----
            var clientData = Selection.activeObject as LLMClientData;
            if (clientData == null)
            {
                Debug.LogError(
                    $"{LogTag} Please select an LLMClientData asset in the Project " +
                    "window before invoking this menu item.");
                return;
            }
            Debug.Log($"{LogTag} Using client data: {clientData.name} (provider: {clientData.Provider})");

            // ---- 2. Build client via factory ----
            ILLMClient client = LLMClientFactory.CreateClient(clientData);
            if (client == null)
            {
                Debug.LogError(
                    $"{LogTag} LLMClientFactory returned null. Check that the client " +
                    "data's provider is supported and that the API key is configured.");
                return;
            }
            Debug.Log($"{LogTag} Client built. Model: {client.Model}, MaxOutputTokens: {client.MaxOutputTokens}");

            // ---- 3. Construct in-memory vocabulary (funk only) ----
            var vocab = BuildSingleGenreVocabulary();
            var lanes = vocab.genres[0].defaultLaneComposition;

            // ---- 4. Build prompt input ----
            var input = new DrumPatternLLMPromptBuilder.Input(
                genreName: "funk",
                subStyleCueName: null,
                timeSignature: TimeSignature.FourFour,
                beatsPerMeasure: 4,
                measures: 2,
                subdivisions: 4,
                laneComposition: lanes,
                userFreeText: null);

            // ---- 5. Sanity-check the built prompts before sending ----
            var build = DrumPatternLLMPromptBuilder.Build(vocab, input);
            if (!build.success)
            {
                Debug.LogError($"{LogTag} Prompt build failed: {build.failureReason}");
                return;
            }
            Debug.Log(
                $"{LogTag} Prompt built. " +
                $"systemPrompt: {build.systemPrompt.Length} chars, " +
                $"userPrompt: {build.userPrompt.Length} chars, " +
                $"total: {build.totalCharCount} chars.");

            // ---- 6. Run generator (async; editor stays responsive) ----
            Debug.Log($"{LogTag} Calling generator (async — editor remains responsive)...");
            float t0 = Time.realtimeSinceStartup;
            DrumPatternLLMGenerator.Result result;
            try
            {
                result = await DrumPatternLLMGenerator.GenerateAsync(client, vocab, input);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Generator threw: {ex.GetType().Name}: {ex.Message}");
                Debug.LogException(ex);
                return;
            }
            float elapsedMs = (Time.realtimeSinceStartup - t0) * 1000f;

            // ---- 7. Log results ----
            LogResult(result, elapsedMs);
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        /// <summary>
        /// Build a single-genre vocabulary (funk) mirroring the test fixture.
        /// Pure-construction; no asset loading required.
        /// </summary>
        private static RhythmGenreVocabularySO BuildSingleGenreVocabulary()
        {
            var vocab = ScriptableObject.CreateInstance<RhythmGenreVocabularySO>();
            vocab.genres = new List<GenreEntry>
            {
                new GenreEntry
                {
                    genreName = "funk",
                    defaultMeter = TimeSignature.FourFour,
                    defaultMeasures = 2,
                    defaultSubdivisions = 4,
                    defaultLaneComposition = new List<LaneSpec>
                    {
                        new LaneSpec { instrument = GeneralMidiPercussion.BassDrum1,     defaultVelocity = 100 },
                        new LaneSpec { instrument = GeneralMidiPercussion.AcousticSnare, defaultVelocity = 110 },
                        new LaneSpec { instrument = GeneralMidiPercussion.ClosedHiHat,   defaultVelocity =  80 },
                        new LaneSpec { instrument = GeneralMidiPercussion.OpenHiHat,     defaultVelocity =  90 },
                    },
                    characteristicCells = new List<GlyphCell>
                    {
                        new GlyphCell { laneIndex = 0, variant = "default", cell = "x..x..x...x....." },
                        new GlyphCell { laneIndex = 1, variant = "default", cell = "....x.......x..." },
                    },
                    subStyleCues = new List<SubStyleCue>(),
                    velocityConventions = "Snare backbeat at lane default. Ghost notes use 'o'. Accents rare.",
                    styleDescriptors    = "Pocket. Syncopation. Ghost notes are the defining gesture.",
                }
            };
            return vocab;
        }

        private static void LogResult(DrumPatternLLMGenerator.Result result, float elapsedMs)
        {
            // ---- Always log raw response for debugging ----
            if (!string.IsNullOrEmpty(result.rawResponse))
                Debug.Log($"{LogTag} Raw LLM response:\n{result.rawResponse}");

            // ---- Failure path ----
            if (!result.success)
            {
                Debug.LogError(
                    $"{LogTag} FAIL after {elapsedMs:0}ms: {result.failureReason} " +
                    $"(InTok: {result.inputTokens}, OutTok: {result.outputTokens})");
                return;
            }

            // ---- Success path ----
            string warningSummary = result.warnings.Count == 0
                ? "no warnings (clean parse)"
                : $"{result.warnings.Count} warning(s)";

            Debug.Log(
                $"{LogTag} SUCCESS in {elapsedMs:0}ms. " +
                $"Lanes: {result.parsedLanes.Length}, totalSteps: {result.totalSteps}, " +
                $"{warningSummary}.");

            Debug.Log(
                $"{LogTag} Token usage — " +
                $"InTok: {result.inputTokens}, OutTok: {result.outputTokens}, " +
                $"Total: {result.inputTokens + result.outputTokens}. " +
                $"(D-L4 tightening: record these for the next roadmap update.)");

            Debug.Log($"{LogTag} Cleaned DSL block:\n{result.cleanedDslBlock}");

            // Per-lane active step counts — quick sanity check
            for (int i = 0; i < result.parsedLanes.Length; i++)
            {
                int active = 0;
                foreach (var step in result.parsedLanes[i])
                {
                    if (step.active) active++;
                }
                Debug.Log(
                    $"{LogTag} Lane {i}: {active}/{result.parsedLanes[i].Count} active steps.");
            }

            // Individual warnings (if any)
            if (result.warnings.Count > 0)
            {
                foreach (var w in result.warnings)
                    Debug.LogWarning($"{LogTag} Parser warning: {w}");
            }
        }
    }
}
#endif