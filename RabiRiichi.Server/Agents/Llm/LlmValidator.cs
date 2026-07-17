using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary> Outcome of validating an LLM config against the live provider. </summary>
  public readonly struct LlmValidationResult(bool ok, string reason) {
    public bool Ok { get; } = ok;
    /// <summary> Short machine-ish reason on failure (e.g. "auth", "timeout"). </summary>
    public string Reason { get; } = reason;

    public static readonly LlmValidationResult Success = new(true, null);
    public static LlmValidationResult Fail(string reason) => new(false, reason);
  }

  /// <summary>
  /// Validates an LLM config by issuing one tiny completion ("ping"). This
  /// catches bad tokens, wrong models, and unreachable URLs up front, so the
  /// room owner gets immediate feedback instead of a silently-broken AI.
  /// </summary>
  public sealed class LlmValidator(ILlmProviderFactory factory) {
    private readonly ILlmProviderFactory factory = factory;

    public async Task<LlmValidationResult> ValidateAsync(
        LlmSettings settings, CancellationToken cancellationToken = default) {
      var provider = factory.Create(settings);
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      cts.CancelAfter(LlmLimits.ValidationTimeout);

      var messages = new List<LlmMessage> {
        LlmMessage.System("You are a connectivity check. Reply with exactly: OK"),
        LlmMessage.User("ping"),
      };
      var result = await provider.CompleteAsync(
          messages, LlmLimits.MaxValidationTokens, cts.Token);

      return result.Success
          ? LlmValidationResult.Success
          : LlmValidationResult.Fail(ClassifyError(result.Error));
    }

    /// <summary> Maps a raw provider error into a short, stable reason code. </summary>
    public static string ClassifyError(string error) {
      if (string.IsNullOrEmpty(error)) {
        return "unknown";
      }
      var e = error.ToLowerInvariant();
      if (e.Contains("timeout")) {
        return "timeout";
      }
      if (e.Contains("401") || e.Contains("403") || e.Contains("unauthor") ||
          e.Contains("permission") || e.Contains("api key") || e.Contains("api_key")) {
        return "auth";
      }
      if (e.Contains("404") || e.Contains("model")) {
        return "model";
      }
      if (e.Contains("429")) {
        return "rate_limit";
      }
      return "unreachable";
    }
  }
}
