#if UNITY_EDITOR
using System.Collections.Generic;
using System.Threading.Tasks;
using BCS.LLM.Core.Clients;

namespace MidiGenPlay.Tests.Editor
{
    /// <summary>
    /// Deterministic <see cref="ILLMClient"/> test double for the L3 SMR-L5 path
    /// (D-L3.2 = A). Returns a caller-supplied <see cref="LLMCompletionResult"/>
    /// from <see cref="CreateChatCompletionAsync(string,string)"/> without any
    /// network call, so an invalid-DSL response can be simulated and routed
    /// through <see cref="DrumPatternLLMGenerator"/> →
    /// <see cref="DrumPatternTextParser"/> exactly as a real response would be.
    /// </summary>
    /// <remarks>
    /// <para>The generator reaches the client via
    /// <c>PromptExecutionHelper.ExecuteAsync(client, prompt, instructions)</c>.
    /// With no attached file IDs (the single-shot D-L10=α path), that helper
    /// delegates to <see cref="CreateChatCompletionAsync(string,string)"/> — so
    /// this double sits on the real call path, not a bypass. Confirmed against
    /// LLM Core <c>PromptExecutionHelper.CreateCompletionMaybeWithFilesAsync</c>.</para>
    /// <para><see cref="WasCalled"/> guards against silent vacuity: if the helper
    /// contract changes and the double is no longer invoked, asserting
    /// <see cref="WasCalled"/> makes the test fail loudly rather than pass on a
    /// path that never exercised the seam.</para>
    /// <para><see cref="ClientConversationHistory"/> is backed by a real, settable
    /// list because <c>PromptExecutionHelper</c> snapshots, clears, and restores
    /// it around the call; a null backing field would NRE inside the helper.</para>
    /// </remarks>
    public sealed class FakeLLMClient : ILLMClient
    {
        private readonly LLMCompletionResult _canned;

        /// <summary>True once <see cref="CreateChatCompletionAsync(string,string)"/> has been invoked.</summary>
        public bool WasCalled { get; private set; }

        /// <summary>The prompt the generator passed on the last call (for assertions).</summary>
        public string LastPrompt { get; private set; }

        /// <summary>The instructions (system prompt) the generator passed on the last call.</summary>
        public string LastInstructions { get; private set; }

        public FakeLLMClient(string outputText, int inputTokens = 100, int outputTokens = 50)
        {
            _canned = new LLMCompletionResult
            {
                OutputText = outputText,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
            };
        }

        // -- Interaction (the only members the generator path exercises) --

        public Task<LLMCompletionResult> CreateChatCompletionAsync(string prompt)
            => CreateChatCompletionAsync(prompt, null);

        public Task<LLMCompletionResult> CreateChatCompletionAsync(string prompt, string instructions)
        {
            WasCalled = true;
            LastPrompt = prompt;
            LastInstructions = instructions;
            return Task.FromResult(_canned);
        }

        // -- Conversation history (touched by PromptExecutionHelper) --

        public List<ChatMessage> ClientConversationHistory { get; set; } = new List<ChatMessage>();
        public void AddMessageToHistory(string role, string content)
            => ClientConversationHistory.Add(new ChatMessage { role = role, content = content });
        public List<KeyValuePair<string, string>> GetFormattedConversationHistory()
            => new List<KeyValuePair<string, string>>();
        public void ClearHistory() => ClientConversationHistory.Clear();

        // -- Remaining interface surface: inert defaults, never read on the test path --

        public string Model { get; set; } = "fake-model";
        public float Temperature { get; set; }
        public int MaxOutputTokens { get; set; } = 800;
        public float TopP { get; set; }
        public float FrequencyPenalty { get; set; }
        public List<string> StopSequences { get; set; } = new List<string>();

        public string SystemInstructions { get; set; } = string.Empty;
        public void ModifySystemInstructions(string instructions) => SystemInstructions = instructions;

        public float InputUSDPerMTokens { get; set; }
        public float CachedInputUSDPerMTokens { get; set; }
        public float OutputUSDPerMTokens { get; set; }
    }
}
#endif