using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// An AI player driven by a large language model. It maintains a compact,
  /// public-info-only transcript (via <c>OnEvent</c>), and on each turn asks the
  /// model to pick from a numbered menu of legal moves and optionally chat/emote.
  ///
  /// Reliability: any timeout, malformed reply, or invalid choice falls back to
  /// the deterministic <see cref="RuleBasedStrategy"/> for the move, and (on the
  /// first fallback in a decision) posts a short in-character apology so the
  /// human players understand the AI "asked a helper". The next decision resends
  /// full context so a transient failure doesn't degrade later play.
  /// </summary>
  public sealed class LlmAI : AIAgent {
    private readonly LlmSettings settings;
    private readonly ILlmProvider provider;
    private readonly LlmEventLog eventLog;

    // Conversation state (guarded by convLock). The transcript is the running
    // system+turns history so the model has multi-turn memory.
    private readonly object convLock = new();
    private readonly List<LlmMessage> transcript = new();
    private bool systemSent;
    private bool roundHeaderSent;

    public LlmAI(int id, Room room, LlmSettings settings, UserStatus status = UserStatus.Playing)
        : base(id, room, status) {
      this.settings = settings;
      // Provider is created eagerly so a misconfiguration surfaces immediately;
      // validation already happened before the agent was admitted.
      this.provider = LlmRuntime.Factory.Create(settings);
      this.eventLog = new LlmEventLog(SeatName);
    }

    /// <summary> Tag for log lines, e.g. "LLM[gemini seat2]". </summary>
    private string LogTag =>
        $"LLM[{LlmDisplayName.ProviderTag(settings.Provider)} seat{Seat}]";

    public override AiType aiType => AiType.Llm;

    // Broadcast nickname: the user's custom name, or a client-localized sentinel
    // (@llm:{provider}) so each human sees the name in their own UI language.
    public override string nickname => LlmDisplayName.NicknameFor(settings);

    // Name used for THIS bot inside prompts, in the bot's own response language.
    private string PromptName =>
        AiLocalization.LlmPromptName(settings.CustomDisplayName, settings.Provider, settings.Language);

    /// <summary> Buffer public events for the next LLM turn. </summary>
    public override void OnEvent(EventBase ev) {
      eventLog.Record(ev);
    }

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      var view = new PublicGameView(gameInquiry.game, Seat);
      var menu = LlmActionMenu.Build(playerInquiry);

      // Degenerate menus (0 or 1 real option) don't need the model.
      if (menu.Count == 0) {
        return InquiryResponse.Default(Seat);
      }

      try {
        var decision = QueryModel(view, menu, remainingTimeout);
        if (decision != null && decision.HasChoice) {
          var chosen = menu.FirstOrDefault(c => c.Id == decision.Choice);
          if (chosen != null) {
            EmitChat(decision);
            return chosen.ToResponse(Seat);
          }
        }
      } catch (Exception e) {
        Logger.Warn($"LLM decision failed: {e.Message}");
      }

      // Fallback: strong deterministic move + a short apology (once).
      return Fallback(view, playerInquiry);
    }

    private LlmDecision QueryModel(
        PublicGameView view, IReadOnlyList<LlmChoice> menu, TimeSpan remainingTimeout) {
      var messages = BuildTurnMessages(view, menu, out var userMessage);

      // Log the outgoing prompt (the fresh turn message; the persistent system +
      // transcript are logged once when first sent, see BuildTurnMessages).
      Logger.Log($"{LogTag} >> prompt:\n{userMessage}");

      using var cts = new CancellationTokenSource(ClampTimeout(remainingTimeout));
      var result = provider
          .CompleteAsync(messages, LlmLimits.MaxDecisionTokens, cts.Token)
          .GetAwaiter().GetResult();

      if (!result.Success) {
        Logger.Warn($"{LogTag} << error: {result.Error}");
        return null;
      }

      Logger.Log($"{LogTag} << response:\n{result.Content}");

      var decision = LlmDecision.Parse(result.Content);
      // Persist the exchange so future turns have memory, but only the user turn
      // and a compact record of the model's own choice (not raw JSON noise).
      lock (convLock) {
        transcript.Add(LlmMessage.User(userMessage));
        transcript.Add(LlmMessage.Assistant(result.Content));
      }
      return decision;
    }

    /// <summary>
    /// Assembles the messages for this turn: the persistent transcript plus the
    /// system prompt (first time) and round header (on a new round), and the
    /// fresh decision message.
    /// </summary>
    private List<LlmMessage> BuildTurnMessages(
        PublicGameView view, IReadOnlyList<LlmChoice> menu, out string userMessage) {
      var builder = new LlmPromptBuilder(settings, SeatNames());
      var recent = eventLog.Drain(out var newRound);

      var messages = new List<LlmMessage>();
      lock (convLock) {
        if (!systemSent) {
          var system = builder.BuildSystemPrompt(Seat);
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
      sb.Append(builder.BuildDecisionPrompt(view, recent, menu));
      userMessage = sb.ToString();

      messages.Add(LlmMessage.User(userMessage));
      return messages;
    }

    private void EmitChat(LlmDecision decision) {
      // The prompt asks the model to chat sparingly; we don't gate server-side.
      // PlayerChatMessage.content is a proto oneof (text XOR sticker), so a
      // single message can carry only one. To show BOTH, send two messages.
      if (!string.IsNullOrWhiteSpace(decision.Say)) {
        room.SendAgentChat(id, decision.Say, null);
      }
      var sticker = StickerRegistry.ResolvePath(decision.Sticker);
      if (sticker != null) {
        room.SendAgentChat(id, null, sticker);
      }
    }

    private InquiryResponse Fallback(PublicGameView view, SinglePlayerInquiry inquiry) {
      // Post a light apology so humans understand the momentary hand-off. Only
      // once per decision (this method is the single fallback path).
      room.SendAgentChat(id, FallbackApology(), null);
      // Force the next decision to resend full context.
      lock (convLock) {
        roundHeaderSent = false;
      }
      return RuleBasedStrategy.Decide(view, inquiry);
    }

    private string FallbackApology() {
      // This is the AI "speaking" in ITS configured response language (same
      // stream as its own chat), so it is localized here rather than client-side
      // — it is not a per-viewer UI string. The fallback move comes from the
      // rule-based bot, so we name-drop it using its language-appropriate name.
      var helper = AiLocalization.AiDisplayName(AiType.RuleBased, settings.Language);
      return settings.Language switch {
        AiLocalization.LangZhs => $"唔……我问问{helper}。",
        AiLocalization.LangJa => $"うーん……{helper}に聞いてみる。",
        _ => $"Hmm... let me ask {helper}.",
      };
    }

    private static TimeSpan ClampTimeout(TimeSpan remaining) {
      var budget = remaining > TimeSpan.Zero && remaining < LlmLimits.RequestTimeout
          ? remaining
          : LlmLimits.RequestTimeout;
      // Leave a small margin so we still have time to compute the fallback.
      var margin = TimeSpan.FromSeconds(2);
      return budget > margin ? budget - margin : budget;
    }

    private string SeatName(int seat) {
      if (seat == Seat) {
        return PromptName;
      }
      var agent = room.GetPlayerBySeat(seat);
      return ResolveName(agent);
    }

    private IReadOnlyDictionary<int, string> SeatNames() {
      var map = new Dictionary<int, string>();
      for (int s = 0; s < room.config.playerCount; s++) {
        map[s] = SeatName(s);
      }
      return map;
    }

    /// <summary>
    /// A friendly, language-appropriate name for another player: humans keep
    /// their nickname; built-in AIs get their localized name so the LLM never
    /// refers to them as "DUMMY"/"RULEBASED".
    /// </summary>
    private string ResolveName(IPlayerAgent agent) {
      if (agent == null) {
        return "?";
      }
      if (agent is User user) {
        return string.IsNullOrWhiteSpace(user.nickname) ? $"P{agent.Seat}" : user.nickname;
      }
      if (agent is LlmAI llm) {
        // Another LLM bot: use its prompt name in THIS bot's language.
        return AiLocalization.LlmPromptName(
            llm.settings.CustomDisplayName, llm.settings.Provider, settings.Language);
      }
      // Built-in AI: localize into this LLM's language (prompt-only).
      var state = agent.GetState();
      return AiLocalization.AiDisplayName(state.AiType, settings.Language);
    }
  }
}
