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
  /// Outcome of one LLM decision turn, including telemetry for per-seat match summaries.
  /// </summary>
  public sealed class ArenaDecision {
    public InquiryResponse Response { get; init; }
    public string ChosenDescription { get; init; } = "";
    public string Rationale { get; init; }
    public string TableChat { get; init; }
    public bool Valid { get; init; }
    public bool Penalized { get; init; }
    public int Attempts { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long ReasoningTokens { get; init; }
    public long LatencyMs { get; init; }
  }

  /// <summary>
  /// Pure retry/validation/penalty state machine for an Arena LLM decision turn.
  /// Tracks token usage, reasoning traces, retries, and latency across attempts.
  /// </summary>
  public sealed class ArenaLlmDecider {
    private readonly ILlmProvider provider;
    private readonly string modelId;
    private readonly int maxAttempts;
    private readonly int maxOutputTokens;
    private readonly TimeSpan decisionTimeout;
    private readonly ReasoningStore reasoning;
    private readonly UsageStats usage;
    private readonly string gameId;

    /// <param name="maxRetries">
    /// Number of additional retries after the initial attempt (0 = 1 attempt total, 2 = up to 3 attempts).
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
      this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
      this.modelId = modelId ?? "";
      this.gameId = gameId ?? "";
      this.maxAttempts = Math.Max(1, maxRetries + 1);
      this.maxOutputTokens = Math.Max(64, maxOutputTokens);
      this.decisionTimeout = decisionTimeout;
      this.reasoning = reasoning;
      this.usage = usage;
    }

    public ArenaDecision Decide(
        int seat,
        IReadOnlyList<LlmChoice> menu,
        SinglePlayerInquiry inquiry,
        int turnSeq,
        IReadOnlyList<LlmMessage> baseTranscript,
        Func<string, string> turnMessageFor,
        TimeSpan remainingTimeout,
        int eventIndex = -1,
        string roundLabel = "") {
      var totalSw = Stopwatch.StartNew();
      string firstPromptDelta = null;
      string lastRaw = null;
      string lastThinking = null;
      string lastError = null;
      string lastRationale = null;
      string lastChat = null;
      long totalPromptTokens = 0;
      long totalCompletionTokens = 0;
      long totalReasoningTokens = 0;
      int attempts = 0;

      for (int attempt = 1; attempt <= maxAttempts; attempt++) {
        attempts = attempt;
        if (attempt > 1) {
          usage?.RecordRetry(modelId);
        }

        var userMessage = turnMessageFor(lastError);
        firstPromptDelta ??= userMessage;

        var messages = new List<LlmMessage>(baseTranscript.Count + 1);
        messages.AddRange(baseTranscript);
        messages.Add(LlmMessage.User(userMessage));

        usage?.RecordRequest(modelId);
        var callSw = Stopwatch.StartNew();
        var result = CallProvider(messages, remainingTimeout);
        callSw.Stop();

        if (!result.Success) {
          lastError = result.Error;
          lastRaw = null;
          usage?.RecordFailure(modelId, ClassifyError(result.Error), callSw.ElapsedMilliseconds);
          continue;
        }

        lastRaw = result.Content;
        lastThinking = result.ReasoningContent;
        totalPromptTokens += result.PromptTokens;
        totalCompletionTokens += result.CompletionTokens;
        totalReasoningTokens += result.ReasoningTokens;

        usage?.RecordSuccess(
            modelId,
            result.PromptTokens,
            result.CompletionTokens,
            result.ReasoningTokens,
            callSw.ElapsedMilliseconds);

        var parsed = ArenaLlmResponse.Parse(result.Content);
        lastRationale = parsed.Rationale;
        lastChat = parsed.Chat;

        var validation = Validate(parsed, menu, seat);
        if (validation.Valid) {
          totalSw.Stop();
          PersistTurn(
              seat, turnSeq, eventIndex, roundLabel, firstPromptDelta,
              lastThinking, result.Content, parsed, validation.Description,
              valid: true, attempts: attempt, penalized: false,
              totalPromptTokens, totalCompletionTokens, totalReasoningTokens,
              totalSw.ElapsedMilliseconds, error: null);

          return new ArenaDecision {
            Response = validation.Response,
            ChosenDescription = validation.Description,
            Rationale = parsed.Rationale,
            TableChat = parsed.Chat,
            Valid = true,
            Penalized = false,
            Attempts = attempt,
            PromptTokens = totalPromptTokens,
            CompletionTokens = totalCompletionTokens,
            ReasoningTokens = totalReasoningTokens,
            LatencyMs = totalSw.ElapsedMilliseconds,
          };
        }

        lastError = validation.Description;
      }

      totalSw.Stop();
      usage?.RecordPenalty(modelId);
      var fallbackParsed = ArenaLlmResponse.Parse(lastRaw ?? "");
      PersistTurn(
          seat, turnSeq, eventIndex, roundLabel, firstPromptDelta ?? "",
          lastThinking, lastRaw ?? "", fallbackParsed, "default (penalized)",
          valid: false, attempts: attempts, penalized: true,
          totalPromptTokens, totalCompletionTokens, totalReasoningTokens,
          totalSw.ElapsedMilliseconds,
          error: lastError ?? "no valid action after retries");

      return new ArenaDecision {
        Response = InquiryResponse.Default(seat),
        ChosenDescription = "default (penalized)",
        Rationale = lastRationale,
        TableChat = lastChat,
        Valid = false,
        Penalized = true,
        Attempts = attempts,
        PromptTokens = totalPromptTokens,
        CompletionTokens = totalCompletionTokens,
        ReasoningTokens = totalReasoningTokens,
        LatencyMs = totalSw.ElapsedMilliseconds,
      };
    }

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
      var budget = decisionTimeout;
      if (remaining > TimeSpan.Zero && remaining < budget) {
        var margin = TimeSpan.FromSeconds(1.5);
        budget = remaining > margin ? remaining - margin : remaining;
      }
      return budget <= TimeSpan.Zero ? decisionTimeout : budget;
    }

    private readonly struct ValidationOutcome(
        bool valid, InquiryResponse response, string description) {
      public bool Valid { get; } = valid;
      public InquiryResponse Response { get; } = response;
      public string Description { get; } = description;
    }

    private static ValidationOutcome Validate(
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

    private void PersistTurn(
        int seat, int turnSeq, int eventIndex, string roundLabel,
        string promptDelta, string thinkingText, string rawResponse,
        ArenaLlmResponse parsed, string parsedAction, bool valid, int attempts,
        bool penalized, long promptTokens, long completionTokens, long reasoningTokens,
        long latencyMs, string error) {
      if (reasoning == null || !ReasoningStore.IsValidGameId(gameId)) {
        return;
      }
      try {
        reasoning.AppendTurn(gameId, seat, new ReasoningTurn {
          TurnSeq = turnSeq,
          EventIndex = eventIndex,
          RoundLabel = roundLabel ?? "",
          PromptDelta = promptDelta ?? "",
          ThinkingText = thinkingText ?? "",
          Reasoning = !string.IsNullOrWhiteSpace(thinkingText) ? thinkingText : (rawResponse ?? ""),
          RawResponse = rawResponse ?? "",
          ParsedAction = parsedAction ?? "",
          Rationale = parsed?.Rationale ?? "",
          TableChat = parsed?.Chat,
          Valid = valid,
          Attempts = attempts,
          Penalized = penalized,
          PromptTokens = promptTokens,
          CompletionTokens = completionTokens,
          ReasoningTokens = reasoningTokens,
          LatencyMs = latencyMs,
          Error = error,
          Timestamp = DateTime.UtcNow.ToString("o"),
        });
      } catch {
        // Persistence must never break gameplay.
      }
    }

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
          || e.Contains("too many requests") || e.Contains("overloaded")) {
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
