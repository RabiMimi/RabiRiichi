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
  /// Uses the rule-based strategy for play and an LLM only for optional table
  /// chat. The selected move is presented to the model as its own decision.
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
      lock (chatLock) {
        pendingChats.Add((++nextChatSequence, new LlmChatEntry(name, text, sticker)));
      }
    }

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      var view = new PublicGameView(gameInquiry.game, Seat);
      var selected = RuleBasedStrategy.Decide(view, playerInquiry);
      if (LlmActionMenu.IsOutOfTurnCallInquiry(playerInquiry)) {
        return selected;
      }
      var menu = LlmActionMenu.Build(playerInquiry, settings.Language);
      var selectedAction = LlmActionMenu.DescribeSelected(
          playerInquiry, selected, settings.Language);
      var automaticActionNote = LlmActionMenu.DescribeAutomaticAction(
          view.SelfRiichi, menu, selectedAction);

      try {
        var chat = QueryModel(view, selectedAction, automaticActionNote, remainingTimeout,
            out var chatsThrough, out var completionSucceeded);
        if (completionSucceeded) {
          // The successful exchange is now in the transcript, so these messages
          // must not be repeated as "new" on the next turn.
          ClearChatsThrough(chatsThrough);
        }
        if (EmitChat(chat)) {
          turnsSinceChat = 0;
          consecutiveChatTurns++;
        } else {
          turnsSinceChat++;
          consecutiveChatTurns = 0;
        }
      } catch (Exception e) {
        turnsSinceChat++;
        consecutiveChatTurns = 0;
        Logger.Warn($"LLM chat failed: {e.Message}");
      }

      // The model response never affects game play.
      return selected;
    }

    private LlmDecision QueryModel(
        PublicGameView view, string selectedAction, string automaticActionNote,
        TimeSpan remainingTimeout,
        out long chatsThrough, out bool completionSucceeded) {
      completionSucceeded = false;
      var messages = BuildTurnMessages(
          view, selectedAction, automaticActionNote,
          out var userMessage, out chatsThrough);
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
        transcript.Add(LlmMessage.User(userMessage));
        transcript.Add(LlmMessage.Assistant(result.Content));
      }
      completionSucceeded = true;
      return LlmDecision.Parse(result.Content);
    }

    private List<LlmMessage> BuildTurnMessages(
        PublicGameView view, string selectedAction, string automaticActionNote,
        out string userMessage, out long chatsThrough) {
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
          turnsSinceChat >= 10,
          LlmPromptBuilder.ShouldSendConsecutiveChatReminder(consecutiveChatTurns)));
      userMessage = sb.ToString();
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

    private Dictionary<int, LlmSeatRole> SeatRoles() {
      var map = new Dictionary<int, LlmSeatRole>();
      for (var seat = 0; seat < room.config.playerCount; seat++) {
        map[seat] = room.GetPlayerBySeat(seat) switch {
          User => LlmSeatRole.Human,
          LlmAI => LlmSeatRole.Llm,
          _ => LlmSeatRole.OtherAi,
        };
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
