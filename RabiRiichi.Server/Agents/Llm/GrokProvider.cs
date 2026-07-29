using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Talks to xAI's Grok API, which serves the OpenAI
  /// <c>/v1/chat/completions</c> dialect at <c>https://api.x.ai</c>. The
  /// transport, auth and response envelope are shared with
  /// <see cref="OpenAiProvider"/> via <see cref="OpenAiCompatible"/>; this class
  /// exists for the two places xAI genuinely differs.
  ///
  /// <b>Token budget.</b> xAI marks <c>max_tokens</c> deprecated in favour of
  /// <c>max_completion_tokens</c>, which counts only visible output and
  /// excludes reasoning tokens. That distinction matters here: our answers are
  /// a few dozen tokens of JSON, and a reasoning model billing its thoughts
  /// against the same small budget would leave nothing for the answer.
  ///
  /// <b>Reasoning.</b> Grok reasoning models think by default - omitting the
  /// parameter is NOT the same as turning it off, and on some models the
  /// default is the most expensive setting. For an in-game seat working against
  /// a 20 second action timeout that would mean timing out every turn and
  /// silently falling back to the rule-based move, so this provider always asks
  /// for the least reasoning the chosen model will accept.
  /// </summary>
  public sealed class GrokProvider(HttpClient http, LlmSettings settings) : ILlmProvider {
    /// <summary> Disables reasoning entirely; only some models accept it. </summary>
    private const string EffortNone = "none";

    /// <summary> Lowest effort every Grok reasoning model accepts. </summary>
    private const string EffortLow = "low";

    /// <summary>
    /// Model families documented to accept <c>reasoning_effort: "none"</c>.
    ///
    /// This is an allowlist rather than a blocklist on purpose: a model we have
    /// never heard of falls back to <see cref="EffortLow"/>, which every
    /// reasoning model accepts. Guessing the other way round would send
    /// <c>none</c> to a model that cannot disable reasoning and get the request
    /// rejected outright.
    ///
    /// Add a family here when xAI documents that its reasoning can be switched
    /// off. Versioned and variant ids (e.g. "grok-4.3-fast") are matched too.
    /// </summary>
    private static readonly string[] ReasoningOptionalFamilies = ["grok-4.3"];

    private readonly HttpClient http = http;
    private readonly LlmSettings settings = settings;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Grok;

    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken) {
      var body = new JsonObject {
        ["model"] = settings.Model,
        // Not max_tokens: xAI deprecates it, and this variant excludes reasoning
        // tokens so the visible JSON answer always gets its full allowance.
        ["max_completion_tokens"] = maxOutputTokens,
        ["temperature"] = 1.0,
        ["reasoning_effort"] = ReasoningEffort(settings.ThinkingLevel, settings.Model),
        ["messages"] = OpenAiCompatible.BuildMessages(messages),
      };
      // presence_penalty, frequency_penalty and stop are rejected by Grok
      // reasoning models, so they are deliberately never sent.

      var exchange = await OpenAiCompatible.PostAsync(
          http, settings.BaseUrl, settings.ApiToken, body, cancellationToken);
      return OpenAiCompatible.ToResult(exchange);
    }

    /// <summary>
    /// Maps a thinking level to xAI's <c>reasoning_effort</c>. Unlike the OpenAI
    /// provider this always returns a value: on Grok, omitting the parameter
    /// means "the model's default", which is the opposite of minimal.
    /// </summary>
    internal static string ReasoningEffort(LlmThinkingLevel level, string model) =>
        level switch {
          LlmThinkingLevel.Low => EffortLow,
          LlmThinkingLevel.Medium => "medium",
          LlmThinkingLevel.High => "high",
          _ => MinimumEffort(model),
        };

    /// <summary> The least reasoning this model will accept. </summary>
    internal static string MinimumEffort(string model) =>
        SupportsDisablingReasoning(model) ? EffortNone : EffortLow;

    /// <summary>
    /// Whether the model is documented to allow reasoning to be turned off.
    /// Matches a family exactly or as a prefix followed by '-', so
    /// "grok-4.3-fast" matches "grok-4.3" but a hypothetical "grok-4.30" does not.
    /// </summary>
    internal static bool SupportsDisablingReasoning(string model) {
      if (string.IsNullOrWhiteSpace(model)) {
        return false;
      }
      var id = model.Trim();
      foreach (var family in ReasoningOptionalFamilies) {
        if (id.Equals(family, StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith(family + "-", StringComparison.OrdinalIgnoreCase)) {
          return true;
        }
      }
      return false;
    }
  }
}
