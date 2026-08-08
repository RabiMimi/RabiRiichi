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

      // A reply proves reachability. So does a well-formed response that carried
      // no visible text (a thinking model can spend a tiny budget entirely on
      // thoughts) — the model is clearly working, so don't reject it here.
      if (result.Success || IsReachableButEmpty(result.Error)) {
        return LlmValidationResult.Success;
      }
      return LlmValidationResult.Fail(ClassifyError(result.Error));
    }

    /// <summary> A 2xx that returned a valid interaction but no visible text. </summary>
    private static bool IsReachableButEmpty(string error) =>
        error != null &&
        error.Contains("no output text", System.StringComparison.OrdinalIgnoreCase);

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
      // A 2xx that yielded no usable text (e.g. a thinking model that spent its
      // whole budget on thoughts, or an unexpected body). Distinct from a
      // genuine connectivity failure so the reason isn't misleading.
      if (e.Contains("empty response") || e.Contains("parse error")) {
        return "no_output";
      }
      return "unreachable";
    }
  }
}
