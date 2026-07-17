using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary> The role of a chat message in an LLM conversation. </summary>
  public enum LlmRole {
    System,
    User,
    Assistant,
  }

  /// <summary> A single message in an LLM conversation. </summary>
  public readonly struct LlmMessage(LlmRole role, string content) {
    public LlmRole Role { get; } = role;
    public string Content { get; } = content;

    public static LlmMessage System(string content) => new(LlmRole.System, content);
    public static LlmMessage User(string content) => new(LlmRole.User, content);
    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);
  }

  /// <summary> The outcome of an LLM completion request. </summary>
  public readonly struct LlmResult(bool success, string content, string error) {
    /// <summary> True if the provider returned a usable completion. </summary>
    public bool Success { get; } = success;
    /// <summary> The raw text content of the completion (may be JSON). </summary>
    public string Content { get; } = content;
    /// <summary> A short human-readable error when <see cref="Success"/> is false. </summary>
    public string Error { get; } = error;

    public static LlmResult Ok(string content) => new(true, content, null);
    public static LlmResult Fail(string error) => new(false, null, error);
  }

  /// <summary>
  /// A minimal, provider-agnostic chat-completion abstraction. Implementations
  /// wrap a specific vendor API (OpenAI-compatible, Gemini, ...). No tool use /
  /// function calling — plain text in, plain text out.
  /// </summary>
  public interface ILlmProvider {
    /// <summary> The provider this instance talks to. </summary>
    ProtoLlmProvider Provider { get; }

    /// <summary>
    /// Sends a chat completion request and returns the model's text response.
    /// Never throws for expected failures (HTTP errors, timeouts, malformed
    /// responses) — those are reported via <see cref="LlmResult.Fail"/>.
    /// </summary>
    Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken);
  }
}
