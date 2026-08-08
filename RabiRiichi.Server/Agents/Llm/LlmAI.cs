using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// An LLM-driven seat: the model picks the move AND talks at the table.
  ///
  /// Each turn the agent builds the list of legal actions, computes what
  /// <see cref="RuleBasedStrategy"/> would do, and shows that to the model as a
  /// reference move it should normally follow. Whatever id the model returns is
  /// what gets played. Anything that cannot be honoured - a missing id, an id
  /// that is not on the menu, a provider error, a timeout, malformed JSON -
  /// falls back to the reference move, so a bad response degrades to solid play
  /// instead of a random or defaulted action.
  /// </summary>
  public sealed class LlmAI : AIAgent {
    private readonly LlmSettings settings;
    private readonly ILlmProvider provider;
    private readonly LlmEventLog eventLog;
    private readonly Lock convLock = new();
    private readonly List<LlmMessage> transcript = new();
    private readonly Lock chatLock = new();
    private readonly List<(long Sequence, LlmChatEntry Entry)> pendingChats = new();
    private bool systemSent;
    private bool roundHeaderSent;
    private int turnsSinceChat;
    private int consecutiveChatTurns;
    private long nextChatSequence;

    public LlmAI(int id, Room room, LlmSettings settings, UserStatus status = UserStatus.Playing)
        : base(id, room, status) {
      this.settings = settings;
      provider = LlmRuntime.Factory.Create(settings);
      eventLog = new LlmEventLog(SeatName, settings.Language);
    }

    private string LogTag =>
        $"LLM[{LlmDisplayName.ProviderTag(settings.Provider)} seat{Seat}]";

    public override AiType aiType => AiType.Llm;
    public override string nickname => LlmDisplayName.NicknameFor(settings);
    private string PromptName => AiLocalization.LlmPromptName(
        settings.CustomDisplayName, settings.Provider, settings.Language);

    public override void OnEvent(EventBase ev) {
      eventLog.Record(ev);
      if (ev is StopGameEvent stopEv && room?.game != null) {
        var view = new PublicGameView(room.game, Seat);
        Task.Run(() => HandleEndGameComment(view, stopEv));
      }
    }

    public override void OnChat(int senderId, string text, string sticker) {
      if (senderId == id)
        return;
      var sender = room.players.FirstOrDefault(p => p.id == senderId);
      var name = sender == null ? $"player {senderId}" : ResolveName(sender);
      var role = sender == null ? (LlmSeatRole?)null : RoleOf(sender);
      lock (chatLock) {
        pendingChats.Add((++nextChatSequence, new LlmChatEntry(name, text, sticker, role)));
      }
    }

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      var view = new PublicGameView(gameInquiry.game, Seat);

      // The reference move, computed before the model is asked so it is always
      // available as a fallback no matter how the call goes.
      var fallback = RuleBasedStrategy.Decide(view, playerInquiry);
      var menu = LlmActionMenu.Build(playerInquiry, settings.Language);
      var reference = LlmActionMenu.Find(menu, fallback);
      var selectedAction = LlmActionMenu.DescribeSelected(menu, playerInquiry, fallback);

      // With one (or no) legal action there is nothing to decide; the model is
      // still asked so it can react to what just happened.
      bool needsDecision = menu.Count > 1;
      var automaticActionNote = needsDecision
          ? null
          : LlmActionMenu.DescribeAutomaticAction(view.SelfRiichi, menu, selectedAction);

      var response = fallback;
      try {
        var decision = QueryModel(
            view,
            needsDecision ? menu : null,
            reference,
            selectedAction,
            automaticActionNote,
            LlmActionMenu.IsOutOfTurnCallInquiry(playerInquiry),
            remainingTimeout,
            out var chatsThrough, out var completionSucceeded);
        if (completionSucceeded) {
          // The successful exchange is now in the transcript, so these messages
          // must not be repeated as "new" on the next turn.
          ClearChatsThrough(chatsThrough);
        }
        if (needsDecision) {
          response = ResolveResponse(decision, menu, fallback, selectedAction);
        }
        if (EmitChat(decision)) {
          turnsSinceChat = 0;
          consecutiveChatTurns++;
        } else {
          turnsSinceChat++;
          consecutiveChatTurns = 0;
        }
      } catch (Exception e) {
        turnsSinceChat++;
        consecutiveChatTurns = 0;
        Logger.Warn($"{LogTag} turn failed, playing the reference move: {e.Message}");
      }

      return response;
    }

    /// <summary>
    /// Turns the model's chosen id into a response. Anything unusable - no id,
    /// or an id that is not on this turn's menu - keeps the reference move, so a
    /// confused model never produces a bad or defaulted action.
    /// </summary>
    private InquiryResponse ResolveResponse(
        LlmDecision decision, IReadOnlyList<LlmChoice> menu,
        InquiryResponse fallback, string fallbackDescription) {
      var chosen = LlmActionMenu.Resolve(menu, decision?.Action);
      if (chosen != null) {
        Logger.Log($"{LogTag} action [{chosen.Id}] {chosen.Description}");
        return chosen.ToResponse(Seat);
      }
      Logger.Warn(decision?.Action == null
          ? $"{LogTag} no action id in response, playing reference: {fallbackDescription}"
          : $"{LogTag} action id {decision.Action} is not on the menu, " +
              $"playing reference: {fallbackDescription}");
      return fallback;
    }

    private LlmDecision QueryModel(
        PublicGameView view, IReadOnlyList<LlmChoice> menu, LlmChoice reference,
        string selectedAction, string automaticActionNote, bool outOfTurnReaction,
        TimeSpan remainingTimeout,
        out long chatsThrough, out bool completionSucceeded) {
      completionSucceeded = false;
      var messages = BuildTurnMessages(
          view, menu, reference, selectedAction, automaticActionNote, outOfTurnReaction,
          out var userMessage, out var historyMessage, out chatsThrough);
      Logger.Log($"{LogTag} >> prompt:\n{userMessage}");

      using var cts = new CancellationTokenSource(ClampTimeout(remainingTimeout));
      var result = provider
          .CompleteAsync(messages, LlmLimits.MaxDecisionTokens, cts.Token)
          .GetAwaiter().GetResult();
      if (!result.Success) {
        Logger.Warn($"{LogTag} << error: {result.Error}");
        return new LlmDecision();
      }

      Logger.Log($"{LogTag} << response:\n{result.Content}");
      lock (convLock) {
        // Store the condensed form, not the prompt we just sent: the position it
        // describes is stale the moment the turn ends, and the next prompt
        // restates it in full.
        transcript.Add(LlmMessage.User(historyMessage));
        transcript.Add(LlmMessage.Assistant(result.Content));
        TrimTranscriptLocked();
      }
      completionSucceeded = true;
      return LlmDecision.Parse(result.Content);
    }

    /// <summary>
    /// Keeps the transcript from growing without limit over a long match.
    ///
    /// This is a rare block cut, not a per-turn slide: nothing happens until the
    /// transcript passes <see cref="LlmLimits.TranscriptMaxExchanges"/>, and then
    /// it drops back to <see cref="LlmLimits.TranscriptTrimToExchanges"/> in one
    /// go. Between cuts every request is a strict extension of the previous one,
    /// which is what lets a stateless provider serve almost all of it from its
    /// prompt cache. The system message is never dropped.
    /// </summary>
    private void TrimTranscriptLocked() {
      // transcript[0] is the system message when one has been sent.
      int firstTurn = systemSent ? 1 : 0;
      int turns = transcript.Count - firstTurn;
      if (turns <= LlmLimits.TranscriptMaxExchanges * 2) {
        return;
      }
      int drop = turns - LlmLimits.TranscriptTrimToExchanges * 2;
      transcript.RemoveRange(firstTurn, drop);
    }

    private List<LlmMessage> BuildTurnMessages(
        PublicGameView view, IReadOnlyList<LlmChoice> menu, LlmChoice reference,
        string selectedAction, string automaticActionNote, bool outOfTurnReaction,
        out string userMessage, out string historyMessage, out long chatsThrough) {
      var builder = new LlmPromptBuilder(settings, SeatNames(), SeatRoles());
      var recent = eventLog.Drain(out var newRound);
      List<LlmChatEntry> chats;
      lock (chatLock) {
        chats = pendingChats.Select(c => c.Entry).ToList();
        chatsThrough = pendingChats.Count == 0 ? 0 : pendingChats[^1].Sequence;
      }

      var messages = new List<LlmMessage>();
      lock (convLock) {
        if (!systemSent) {
          var system = builder.BuildSystemPrompt(Seat, view);
          transcript.Add(LlmMessage.System(system));
          systemSent = true;
          Logger.Log($"{LogTag} >> system prompt:\n{system}");
        }
        messages.AddRange(transcript);
      }

      var sb = new System.Text.StringBuilder();
      if (newRound || !roundHeaderSent) {
        sb.AppendLine(builder.BuildRoundHeader(view));
        roundHeaderSent = true;
      }
      sb.Append(builder.BuildDecisionPrompt(
          view, recent, selectedAction, automaticActionNote, chats,
          turnsSinceChat >= 5,
          LlmPromptBuilder.ShouldSendConsecutiveChatReminder(consecutiveChatTurns),
          menu, reference, outOfTurnReaction));
      userMessage = sb.ToString();
      historyMessage = builder.BuildHistorySummary(view, chats);
      messages.Add(LlmMessage.User(userMessage));
      return messages;
    }


    private bool EmitChat(LlmDecision decision) {
      if (decision == null)
        return false;
      var emitted = false;
      if (!string.IsNullOrWhiteSpace(decision.Say)) {
        room.SendAgentChat(this, decision.Say, null);
        emitted = true;
      }
      var sticker = StickerRegistry.ResolvePath(decision.Sticker);
      if (sticker != null) {
        room.SendAgentChat(this, null, sticker);
        emitted = true;
      }
      return emitted;
    }

    private void ClearChatsThrough(long sequence) {
      if (sequence <= 0)
        return;
      lock (chatLock) {
        pendingChats.RemoveAll(c => c.Sequence <= sequence);
      }
    }

    private static TimeSpan ClampTimeout(TimeSpan remaining) {
      var budget = remaining > TimeSpan.Zero && remaining < LlmLimits.RequestTimeout
          ? remaining
          : LlmLimits.RequestTimeout;
      var margin = TimeSpan.FromSeconds(2);
      return budget > margin ? budget - margin : budget;
    }

    private string SeatName(int seat) {
      if (seat == Seat)
        return PromptName;
      return ResolveName(room.GetPlayerBySeat(seat));
    }

    private Dictionary<int, string> SeatNames() {
      var map = new Dictionary<int, string>();
      for (var seat = 0; seat < room.config.playerCount; seat++) {
        map[seat] = SeatName(seat);
      }
      return map;
    }

    private static LlmSeatRole RoleOf(IPlayerAgent agent) => agent switch {
      User => LlmSeatRole.Human,
      LlmAI => LlmSeatRole.Llm,
      _ => LlmSeatRole.OtherAi,
    };

    private Dictionary<int, LlmSeatRole> SeatRoles() {
      var map = new Dictionary<int, LlmSeatRole>();
      for (var seat = 0; seat < room.config.playerCount; seat++) {
        map[seat] = RoleOf(room.GetPlayerBySeat(seat));
      }
      return map;
    }

    private string ResolveName(IPlayerAgent agent) {
      if (agent == null)
        return "?";
      if (agent is User user) {
        return string.IsNullOrWhiteSpace(user.nickname) ? $"P{agent.Seat}" : user.nickname;
      }
      if (agent is LlmAI llm) {
        return AiLocalization.LlmPromptName(
            llm.settings.CustomDisplayName, llm.settings.Provider, settings.Language);
      }
      return AiLocalization.AiDisplayName(agent.GetState().AiType, settings.Language);
    }

    private void HandleEndGameComment(PublicGameView view, StopGameEvent stopEv) {
      try {
        var recent = eventLog.Drain(out _);
        List<LlmChatEntry> chats;
        long chatsThrough;
        lock (chatLock) {
          chats = [.. pendingChats.Select(c => c.Entry)];
          chatsThrough = pendingChats.Count == 0 ? 0 : pendingChats[^1].Sequence;
        }

        var builder = new LlmPromptBuilder(settings, SeatNames(), SeatRoles());
        var userMessage = builder.BuildEndGamePrompt(view, recent, chats, stopEv.endGamePoints);

        var messages = new List<LlmMessage>();
        lock (convLock) {
          if (!systemSent) {
            var system = builder.BuildSystemPrompt(Seat, view);
            transcript.Add(LlmMessage.System(system));
            systemSent = true;
            Logger.Log($"{LogTag} >> system prompt:\n{system}");
          }
          messages.AddRange(transcript);
        }
        messages.Add(LlmMessage.User(userMessage));

        Logger.Log($"{LogTag} >> end-game prompt:\n{userMessage}");
        using var cts = new CancellationTokenSource(LlmLimits.RequestTimeout);
        var result = provider
            .CompleteAsync(messages, LlmLimits.MaxDecisionTokens, cts.Token)
            .GetAwaiter().GetResult();

        if (!result.Success) {
          Logger.Warn($"{LogTag} << end-game error: {result.Error}");
          return;
        }

        Logger.Log($"{LogTag} << end-game response:\n{result.Content}");
        lock (convLock) {
          transcript.Add(LlmMessage.User(userMessage));
          transcript.Add(LlmMessage.Assistant(result.Content));
          TrimTranscriptLocked();
        }
        ClearChatsThrough(chatsThrough);
        var decision = LlmDecision.Parse(result.Content);
        EmitChat(decision);
      } catch (Exception e) {
        Logger.Warn($"{LogTag} end-game chat failed: {e.Message}");
      }
    }
  }
}
