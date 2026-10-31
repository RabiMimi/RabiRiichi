using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Full-decision LLM Mahjong agent for Arena evaluations.
  ///
  /// Key evaluation behaviors:
  ///  1. Skips redundant LLM calls when <c>menu.Count == 1</c> (e.g. <c>next_round</c>
  ///     acknowledgement or forced tsumogiri after Riichi), cutting latency &amp; cost.
  ///  2. Honors <c>Prompts.AskLlmForCalls</c> for out-of-turn chii/pon/daiminkan opportunities.
  ///  3. Maintains a bounded condensed turn history (<c>Prompts.MaxHistoryTurns</c>) so
  ///     multi-round Hanchan games retain strategic memory without context bloat.
  ///  4. Accurately tracks per-seat decision, retry, penalty, token, and latency metrics.
  /// </summary>
  public class ArenaLlmAI : AIAgent {
    private sealed record CondensedTurn(string UserSummary, string AssistantJson);

    private readonly ArenaConfig.ModelConfig model;
    private readonly ArenaConfig.DecisionConfig decision;
    private readonly ArenaConfig.PromptConfig prompts;
    private readonly ILlmProvider provider;
    private readonly ReasoningStore reasoning;
    private readonly UsageStats usage;
    private readonly string gameId;
    private readonly Func<int, Wind> windOf;
    private readonly Func<int, string> displayNameOf;

    private readonly Lock convLock = new();
    private string systemPromptText;
    private readonly List<CondensedTurn> history = new();
    private readonly Lock chatLock = new();
    private readonly List<ArenaChatLine> pendingChats = new();

    private LlmEventLog eventLog;
    private ArenaSeatLabeler labeler;
    private ArenaPromptBuilder promptBuilder;
    private bool initialized;
    private bool metaWritten;
    private int turnSeq;

    // Per-match seat telemetry counters (read by EvalRoom after game end).
    private int decisionsCount;
    private int autoDecisionsCount;
    private int validFirstTryCount;
    private int retryCount;
    private int penaltyCount;
    private long promptTokens;
    private long completionTokens;
    private long reasoningTokens;
    private long totalLatencyMs;
    private long maxLatencyMs;

    public ArenaLlmAI(
        int id,
        Room room,
        ArenaConfig.ModelConfig model,
        ArenaConfig.DecisionConfig decision,
        ArenaConfig.PromptConfig prompts,
        ILlmProvider provider,
        ReasoningStore reasoning,
        UsageStats usage,
        string gameId,
        Func<int, Wind> windOf,
        Func<int, string> displayNameOf = null,
        UserStatus status = UserStatus.Playing)
        : base(id, room, status) {
      this.model = model ?? throw new ArgumentNullException(nameof(model));
      this.decision = decision ?? new ArenaConfig.DecisionConfig();
      this.prompts = prompts ?? new ArenaConfig.PromptConfig();
      this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
      this.reasoning = reasoning;
      this.usage = usage;
      this.gameId = gameId;
      this.windOf = windOf;
      this.displayNameOf = displayNameOf;
    }

    public override AiType aiType => AiType.Llm;

    public override string nickname =>
        prompts.RevealOpponentIdentity && !string.IsNullOrWhiteSpace(model.DisplayName)
            ? model.DisplayName
            : $"Player {Seat + 1}";

    // Telemetry accessors for EvalRoom.
    public int DecisionsCount => Volatile.Read(ref decisionsCount);
    public int AutoDecisionsCount => Volatile.Read(ref autoDecisionsCount);
    public int ValidFirstTryCount => Volatile.Read(ref validFirstTryCount);
    public int RetryCount => Volatile.Read(ref retryCount);
    public int PenaltyCount => Volatile.Read(ref penaltyCount);
    public long PromptTokens => Interlocked.Read(ref promptTokens);
    public long CompletionTokens => Interlocked.Read(ref completionTokens);
    public long ReasoningTokens => Interlocked.Read(ref reasoningTokens);
    public long TotalLatencyMs => Interlocked.Read(ref totalLatencyMs);
    public long MaxLatencyMs => Interlocked.Read(ref maxLatencyMs);

    public override void OnEvent(EventBase ev) {
      EnsureInitialized();
      eventLog?.Record(ev);
    }

    public override void OnChat(int senderId, string text, string sticker) {
      if (!prompts.EnableTableTalk) {
        return;
      }
      if (senderId == id || string.IsNullOrWhiteSpace(text)) {
        return;
      }
      EnsureInitialized();
      var senderSeat = SeatOfId(senderId);
      var sender = senderSeat >= 0 && labeler != null
          ? labeler.ChatSenderLabel(senderSeat)
          : $"Player {senderId}";
      lock (chatLock) {
        pendingChats.Add(new ArenaChatLine(sender, text));
      }
    }

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      EnsureInitialized();
      var view = new PublicGameView(gameInquiry.game, Seat);

      // Optional fast-pass when operator disables LLM out-of-turn call queries
      // (Ron is never skipped by this flag since IsOutOfTurnCallInquiry returns false when Ron is available).
      if (!prompts.AskLlmForCalls && LlmActionMenu.IsOutOfTurnCallInquiry(playerInquiry)) {
        Interlocked.Increment(ref autoDecisionsCount);
        return InquiryResponse.Default(Seat);
      }

      var menu = LlmActionMenu.Build(playerInquiry, EffectiveLanguage, annotateTenpai: false);
      if (menu.Count == 0) {
        return InquiryResponse.Default(Seat);
      }

      // Fast-path forced single-option inquiries (e.g. next_round ack or forced Riichi tsumogiri).
      if (menu.Count == 1) {
        Interlocked.Increment(ref autoDecisionsCount);
        return menu[0].ToResponse(Seat);
      }

      EnsureMeta(view);

      var recent = eventLog?.Drain(out _) ?? Array.Empty<string>();
      List<ArenaChatLine> chats;
      lock (chatLock) {
        chats = pendingChats.ToList();
        pendingChats.Clear();
      }

      List<LlmMessage> baseTranscript = BuildTranscriptSnapshot();
      int maxOutTokens = model.MaxOutputTokens ?? decision.MaxOutputTokens;

      var decider = new ArenaLlmDecider(
          provider, model.Id, gameId, decision.MaxRetries,
          maxOutTokens, TimeSpan.FromSeconds(decision.TimeoutSeconds),
          reasoning, usage);

      var seq = Interlocked.Increment(ref turnSeq);
      var result = decider.Decide(
          Seat, menu, playerInquiry, seq, baseTranscript,
          error => promptBuilder.BuildTurnMessage(view, menu, recent, chats, error),
          remainingTimeout,
          CurrentReplayEventIndex(),
          ArenaPromptBuilder.RoundLabelOf(view));

      RecordTelemetry(result);

      if (result.Valid && prompts.MaxHistoryTurns > 0) {
        var condensedUser = promptBuilder.BuildCondensedTurnSummary(view, result.ChosenDescription);
        var assistantJson = JsonSerializer.Serialize(new {
          action = menu.FirstOrDefault(c => c.Matches(result.Response))?.Id ?? 0,
          rationale = result.Rationale ?? "",
        });
        lock (convLock) {
          history.Add(new CondensedTurn(condensedUser, assistantJson));
          int cap = Math.Max(0, prompts.MaxHistoryTurns);
          if (history.Count > cap) {
            history.RemoveRange(0, history.Count - cap);
          }
        }
      }

      EmitTableChat(result);
      return result.Response;
    }

    private List<LlmMessage> BuildTranscriptSnapshot() {
      lock (convLock) {
        var list = new List<LlmMessage>(1 + history.Count * 2);
        if (!string.IsNullOrEmpty(systemPromptText)) {
          list.Add(LlmMessage.System(systemPromptText));
        }
        foreach (var h in history) {
          list.Add(LlmMessage.User(h.UserSummary));
          list.Add(LlmMessage.Assistant(h.AssistantJson));
        }
        return list;
      }
    }

    private void RecordTelemetry(ArenaDecision result) {
      Interlocked.Increment(ref decisionsCount);
      if (result.Valid && result.Attempts == 1) {
        Interlocked.Increment(ref validFirstTryCount);
      }
      if (result.Attempts > 1) {
        Interlocked.Add(ref retryCount, result.Attempts - 1);
      }
      if (result.Penalized) {
        Interlocked.Increment(ref penaltyCount);
      }
      Interlocked.Add(ref promptTokens, result.PromptTokens);
      Interlocked.Add(ref completionTokens, result.CompletionTokens);
      Interlocked.Add(ref reasoningTokens, result.ReasoningTokens);
      Interlocked.Add(ref totalLatencyMs, result.LatencyMs);
      long prevMax;
      do {
        prevMax = Interlocked.Read(ref maxLatencyMs);
        if (result.LatencyMs <= prevMax) break;
      } while (Interlocked.CompareExchange(ref maxLatencyMs, result.LatencyMs, prevMax) != prevMax);
    }

    private string EffectiveLanguage =>
        ArenaPromptBuilder.NormalizeArenaLanguage(
            !string.IsNullOrWhiteSpace(model.PromptLanguage) ? model.PromptLanguage : prompts.Language);

    private void EnsureInitialized() {
      if (initialized) {
        return;
      }
      lock (convLock) {
        if (initialized) {
          return;
        }
        var seat = Seat;
        var lang = EffectiveLanguage;
        labeler = new ArenaSeatLabeler(
            prompts.RevealOpponentIdentity, seat, ResolveWind, displayNameOf, lang);
        promptBuilder = new ArenaPromptBuilder(labeler, prompts, lang);
        eventLog = new LlmEventLog(s => labeler.Label(s), lang);
        initialized = true;
      }
    }

    private void EnsureMeta(PublicGameView view) {
      if (metaWritten) {
        return;
      }
      var systemPrompt = promptBuilder.BuildSystemPrompt(Seat, view);
      lock (convLock) {
        if (!metaWritten) {
          systemPromptText = systemPrompt;
          metaWritten = true;
        }
      }
      if (reasoning != null && ReasoningStore.IsValidGameId(gameId)) {
        try {
          reasoning.WriteMeta(new ReasoningMeta {
            GameId = gameId,
            Seat = Seat,
            ModelId = model.Id,
            DisplayName = model.DisplayName ?? model.Id,
            Provider = model.Provider,
            Model = model.Model ?? "",
            ThinkingLevel = model.ThinkingLevel ?? "minimal",
            SystemPrompt = systemPrompt,
            CreatedAt = DateTime.UtcNow.ToString("o"),
          });
        } catch {
          // Persistence must never break gameplay.
        }
      }
    }

    private int CurrentReplayEventIndex() =>
        (room?.config?.actionCenter as ServerActionCenter)?.CapturedEventCount ?? -1;

    private void EmitTableChat(ArenaDecision result) {
      string chatLine = prompts.EnableTableTalk && !string.IsNullOrWhiteSpace(result.TableChat)
          ? result.TableChat
          : result.Rationale;
      if (string.IsNullOrWhiteSpace(chatLine)) {
        return;
      }
      try {
        room.SendAgentChat(this, chatLine, null);
      } catch (Exception e) {
        Logger.Warn($"Arena chat broadcast failed: {e.Message}");
      }
    }

    private Wind ResolveWind(int seat) {
      if (windOf != null) {
        return windOf(seat);
      }
      var game = room?.game;
      return game != null ? game.GetPlayer(seat).Wind : Wind.E;
    }

    private int SeatOfId(int senderId) {
      var agent = room?.players?.FirstOrDefault(p => p.id == senderId);
      return agent?.Seat ?? -1;
    }
  }
}
