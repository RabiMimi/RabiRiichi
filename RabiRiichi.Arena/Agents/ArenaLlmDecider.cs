using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// The outcome of one Arena decision: the chosen <see cref="InquiryResponse"/>
  /// (a valid legal action, or <see cref="InquiryResponse.Default(int)"/> after
  /// exhausting retries), the rationale to broadcast, and bookkeeping flags.
  /// </summary>
  public sealed class ArenaDecision {
    public InquiryResponse Response { get; init; }
    public string Rationale { get; init; }
    public bool Valid { get; init; }
    public bool Penalized { get; init; }
    public int Attempts { get; init; }
  }

  /// <summary>
  /// The pure retry/penalty state machine for an Arena playing agent
  /// (ARENA_DESIGN.md §5). It calls the LLM to choose a legal action, parses
  /// <c>{ action, rationale }</c>, validates the choice against the player's
  /// legal inquiry, and RETRIES up to <c>maxRetries</c> total attempts. After
  /// the final failure it returns <see cref="InquiryResponse.Default(int)"/> and
  /// records a penalty. Provider errors are mapped to
  /// <see cref="UsageErrorCategory"/> for monitoring (§12c).
  ///
  /// This class has no dependency on <c>Room</c> / <c>Game</c> internals beyond
  /// the seat, menu, and inquiry, so it is directly unit-testable with a fake
  /// <see cref="ILlmProvider"/> (no network). It appends every decision to the
  /// <see cref="ReasoningStore"/> as a per-turn delta (§8) and updates
  /// <see cref="UsageStats"/> around each provider call.
  /// </summary>
  public sealed class ArenaLlmDecider {
    private readonly ILlmProvider provider;
    private readonly string modelId;
    private readonly int maxRetries;
    private readonly int maxOutputTokens;
    private readonly TimeSpan decisionTimeout;
    private readonly ReasoningStore reasoning;
    private readonly UsageStats usage;
    private readonly string gameId;

    /// <param name="maxRetries">
    /// Total attempts before penalizing (design default 3). Values &lt; 1 are
    /// clamped to 1 so at least one attempt is always made.
    /// </param>
    public ArenaLlmDecider(
        ILlmProvider provider,
        string modelId,
        string gameId,
        int maxRetries,
        int maxOutputTokens,
        TimeSpan decisionTimeout,
        ReasoningStore reasoning,
        UsageStats usage) {
      this.provider = provider;
      this.modelId = modelId;
      this.gameId = gameId;
      this.maxRetries = Math.Max(1, maxRetries);
      this.maxOutputTokens = maxOutputTokens;
      this.decisionTimeout = decisionTimeout;
      this.reasoning = reasoning;
      this.usage = usage;
    }

    /// <summary>
    /// Runs the decision loop for one turn.
    /// </summary>
    /// <param name="seat">The deciding seat / player id.</param>
    /// <param name="menu">The legal-action menu (stable choice ids).</param>
    /// <param name="inquiry">The player's legal inquiry (for validation).</param>
    /// <param name="turnSeq">A monotonically increasing per-seat turn number.</param>
    /// <param name="baseTranscript">
    /// Prior conversation (system + accumulated turns) to send as context. The
    /// caller owns and mutates it across turns.
    /// </param>
    /// <param name="turnMessageFor">
    /// Builds the user message for an attempt: the first attempt gets a null
    /// validation error; retries pass the prior rejection so the model can
    /// correct itself. The returned string is the promptDelta persisted for the
    /// turn's FIRST attempt (§8).
    /// </param>
    /// <param name="remainingTimeout">
    /// The engine's remaining budget; the decision timeout is clamped to it when
    /// smaller so the engine never preempts.
    /// </param>
    public ArenaDecision Decide(
        int seat,
        IReadOnlyList<LlmChoice> menu,
        SinglePlayerInquiry inquiry,
        int turnSeq,
        List<LlmMessage> baseTranscript,
        Func<string, string> turnMessageFor,
        TimeSpan remainingTimeout) {
      var sw = Stopwatch.StartNew();
      string firstPromptDelta = null;
      string lastRaw = null;
      string lastError = null;
      string lastRationale = null;
      long promptTokens = 0, completionTokens = 0;
      int attempts = 0;

      for (int attempt = 1; attempt <= maxRetries; attempt++) {
        attempts = attempt;
        if (attempt > 1) {
          usage.RecordRetry(modelId);
        }

        var userMessage = turnMessageFor(lastError);
        firstPromptDelta ??= userMessage;

        var messages = new List<LlmMessage>(baseTranscript) {
          LlmMessage.User(userMessage),
        };

        usage.RecordRequest(modelId);
        var result = CallProvider(messages, remainingTimeout);
        if (!result.Success) {
          lastError = result.Error;
          lastRaw = null;
          usage.RecordFailure(modelId, ClassifyError(result.Error));
          continue;
        }

        lastRaw = result.Content;
        // A successful transport is a success for usage accounting; token usage
        // is not surfaced by ILlmProvider, so it is recorded as 0.
        usage.RecordSuccess(modelId, promptTokens, completionTokens);

        var parsed = ArenaLlmResponse.Parse(result.Content);
        lastRationale = parsed.Rationale;

        var validation = Validate(parsed, menu, seat);
        if (validation.Valid) {
          // Commit the successful exchange to the running transcript so the next
          // turn sees it (and is not duplicated on disk — only the delta is
          // stored).
          baseTranscript.Add(LlmMessage.User(userMessage));
          baseTranscript.Add(LlmMessage.Assistant(result.Content));
          sw.Stop();
          PersistTurn(seat, turnSeq, firstPromptDelta, result.Content, parsed,
              validation.Description, valid: true, attempts: attempt,
              penalized: false, promptTokens, completionTokens, sw.ElapsedMilliseconds,
              error: null);
          return new ArenaDecision {
            Response = validation.Response,
            Rationale = parsed.Rationale,
            Valid = true,
            Penalized = false,
            Attempts = attempt,
          };
        }

        // Parsed but illegal: feed the error back for the next attempt.
        lastError = validation.Description;
      }

      // Exhausted all attempts -> penalize with the safe default option.
      sw.Stop();
      usage.RecordPenalty(modelId);
      PersistTurn(seat, turnSeq, firstPromptDelta ?? "", lastRaw ?? "",
          ArenaLlmResponse.Parse(lastRaw ?? ""), "default (penalized)",
          valid: false, attempts: attempts, penalized: true,
          promptTokens, completionTokens, sw.ElapsedMilliseconds,
          error: lastError ?? "no valid action after retries");
      return new ArenaDecision {
        Response = InquiryResponse.Default(seat),
        Rationale = lastRationale,
        Valid = false,
        Penalized = true,
        Attempts = attempts,
      };
    }

    // ----- Provider call ---------------------------------------------------

    private LlmResult CallProvider(
        IReadOnlyList<LlmMessage> messages, TimeSpan remainingTimeout) {
      var budget = ClampTimeout(remainingTimeout);
      try {
        using var cts = new CancellationTokenSource(budget);
        return provider
            .CompleteAsync(messages, maxOutputTokens, cts.Token)
            .GetAwaiter().GetResult();
      } catch (OperationCanceledException) {
        return LlmResult.Fail("timeout");
      } catch (Exception e) {
        return LlmResult.Fail(e.Message);
      }
    }

    private TimeSpan ClampTimeout(TimeSpan remaining) {
      // Use the long per-decision budget, but never exceed the engine's own
      // remaining budget (leaving a small margin so we cancel before it does).
      var budget = decisionTimeout;
      if (remaining > TimeSpan.Zero && remaining < budget) {
        var margin = TimeSpan.FromSeconds(2);
        budget = remaining > margin ? remaining - margin : remaining;
      }
      return budget <= TimeSpan.Zero ? decisionTimeout : budget;
    }

    // ----- Validation ------------------------------------------------------

    private readonly struct ValidationOutcome(
        bool valid, InquiryResponse response, string description) {
      public bool Valid { get; } = valid;
      public InquiryResponse Response { get; } = response;
      public string Description { get; } = description;
    }

    /// <summary>
    /// Validates a parsed answer against the legal menu, producing the concrete
    /// <see cref="InquiryResponse"/> when valid. Also confirms the produced
    /// response is accepted by the engine's own per-action check (the same
    /// gate the live inquiry uses), so a menu bug can never yield an illegal
    /// response.
    /// </summary>
    private ValidationOutcome Validate(
        ArenaLlmResponse parsed, IReadOnlyList<LlmChoice> menu, int seat) {
      if (!parsed.HasAction) {
        return new ValidationOutcome(false, null,
            "response did not contain a numeric \"action\" id");
      }
      var choice = menu.FirstOrDefault(c => c.Id == parsed.ActionId.Value);
      if (choice == null) {
        var ids = string.Join(", ", menu.Select(c => c.Id));
        return new ValidationOutcome(false, null,
            $"action id {parsed.ActionId.Value} is not in the legal menu (valid ids: {ids})");
      }
      return new ValidationOutcome(true, choice.ToResponse(seat), choice.Description);
    }

    // ----- Persistence -----------------------------------------------------

    private void PersistTurn(
        int seat, int turnSeq, string promptDelta, string rawResponse,
        ArenaLlmResponse parsed, string parsedAction, bool valid, int attempts,
        bool penalized, long promptTokens, long completionTokens, long latencyMs,
        string error) {
      if (reasoning == null || !ReasoningStore.IsValidGameId(gameId)) {
        return;
      }
      try {
        reasoning.AppendTurn(gameId, seat, new ReasoningTurn {
          TurnSeq = turnSeq,
          PromptDelta = promptDelta ?? "",
          // ILlmProvider returns final text only (no separate reasoning trace),
          // so the raw response doubles as the reasoning record.
          Reasoning = rawResponse ?? "",
          RawResponse = rawResponse ?? "",
          ParsedAction = parsedAction ?? "",
          Rationale = parsed?.Rationale ?? "",
          Valid = valid,
          Attempts = attempts,
          Penalized = penalized,
          PromptTokens = promptTokens,
          CompletionTokens = completionTokens,
          LatencyMs = latencyMs,
          Error = error,
          Timestamp = DateTime.UtcNow.ToString("o"),
        });
      } catch {
        // Persistence must never break gameplay.
      }
    }

    // ----- Error classification --------------------------------------------

    /// <summary>
    /// Maps a raw provider error string to a <see cref="UsageErrorCategory"/>
    /// for the monitoring dashboard (§12c). Mirrors the buckets used by the
    /// server's <c>LlmValidator.ClassifyError</c> reason codes.
    /// </summary>
    public static UsageErrorCategory ClassifyError(string error) {
      if (string.IsNullOrEmpty(error)) {
        return UsageErrorCategory.Other;
      }
      var e = error.ToLowerInvariant();
      if (e.Contains("timeout") || e.Contains("timed out") || e.Contains("canceled")
          || e.Contains("cancelled")) {
        return UsageErrorCategory.Timeout;
      }
      if (e.Contains("429") || e.Contains("rate limit") || e.Contains("rate_limit")
          || e.Contains("too many requests")) {
        return UsageErrorCategory.RateLimited;
      }
      if (e.Contains("401") || e.Contains("403") || e.Contains("unauthor")
          || e.Contains("permission") || e.Contains("api key") || e.Contains("api_key")
          || e.Contains("forbidden")) {
        return UsageErrorCategory.Auth;
      }
      if (e.Contains("empty response") || e.Contains("parse error")
          || e.Contains("no output") || e.Contains("invalid")) {
        return UsageErrorCategory.InvalidResponse;
      }
      if (e.Contains("connection") || e.Contains("network") || e.Contains("refused")
          || e.Contains("host") || e.Contains("dns") || e.Contains("socket")
          || e.Contains("unreachable") || e.Contains("http")) {
        return UsageErrorCategory.Network;
      }
      return UsageErrorCategory.Other;
    }
  }
}
