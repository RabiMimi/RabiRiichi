using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RabiRiichi.Actions;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// An LLM agent that actually PLAYS: its <see cref="Decide"/> asks the model to
  /// choose a legal action (unlike the server's <c>LlmAI</c>, which plays with the
  /// rule-based strategy and uses the LLM only for chat). See ARENA_DESIGN.md
  /// §5/§6/§7/§8/§9/§9a. It:
  ///  - builds a cheat-safe <see cref="PublicGameView"/> and a pre-computed tool
  ///    context + legal-action menu;
  ///  - calls the provider with a LONG per-decision timeout (from
  ///    <see cref="ArenaConfig.DecisionConfig"/>), not the server's 20s limit;
  ///  - validates + retries up to <c>maxRetries</c>, then penalizes with
  ///    <see cref="InquiryResponse.Default(int)"/>;
  ///  - broadcasts the rationale as agent chat (always kept in the replay);
  ///  - persists every decision to the <see cref="ReasoningStore"/> and updates
  ///    <see cref="UsageStats"/>.
  ///
  /// Exposure gates (both from <see cref="ArenaConfig.ExposureConfig"/>):
  ///  - <c>chatToAgents</c>: when false (default), <see cref="OnChat"/> is a
  ///    no-op so no other agent's chat ever folds into this agent's prompt; when
  ///    true, incoming chat is recorded (with an anonymity-safe sender label) for
  ///    the next turn's context.
  ///  - <c>revealOpponentIdentity</c>: when false (default), opponents appear in
  ///    the prompt/menu/tool-context ONLY by neutral seat label; when true, real
  ///    display names may appear. Enforced via <see cref="ArenaSeatLabeler"/>.
  /// </summary>
  public class ArenaLlmAI : AIAgent {
    private readonly ArenaConfig.ModelConfig model;
    private readonly ArenaConfig.DecisionConfig decision;
    private readonly ArenaConfig.ExposureConfig exposure;
    private readonly ILlmProvider provider;
    private readonly ReasoningStore reasoning;
    private readonly UsageStats usage;
    private readonly string gameId;
    private readonly Func<int, Wind> windOf;
    private readonly Func<int, string> displayNameOf;

    private readonly Lock convLock = new();
    private readonly List<LlmMessage> transcript = new();
    private readonly Lock chatLock = new();
    private readonly List<ArenaChatLine> pendingChats = new();

    private LlmEventLog eventLog;
    private ArenaSeatLabeler labeler;
    private ArenaPromptBuilder promptBuilder;
    private bool initialized;
    private bool metaWritten;
    private int turnSeq;

    /// <param name="model">Roster entry for this agent (provider/model/etc.).</param>
    /// <param name="decision">Long timeout + retry/penalty policy.</param>
    /// <param name="exposure">Opponent-anonymity + chat-visibility gates.</param>
    /// <param name="provider">
    /// The LLM provider (built via <c>LlmProviderFactory</c>); injected so tests
    /// can supply a fake with no network.
    /// </param>
    /// <param name="reasoning">Per-decision reasoning persistence (§8).</param>
    /// <param name="usage">Per-model usage counters (§12c).</param>
    /// <param name="gameId">Reasoning artifact key; must be path-safe.</param>
    /// <param name="windOf">Maps a seat to its seat wind (neutral labels).</param>
    /// <param name="displayNameOf">
    /// Maps a seat to its real display name; only consulted when
    /// <c>revealOpponentIdentity</c> is true. May be null under anonymity.
    /// </param>
    public ArenaLlmAI(
        int id,
        Room room,
        ArenaConfig.ModelConfig model,
        ArenaConfig.DecisionConfig decision,
        ArenaConfig.ExposureConfig exposure,
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
      this.exposure = exposure ?? new ArenaConfig.ExposureConfig();
      this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
      this.reasoning = reasoning;
      this.usage = usage;
      this.gameId = gameId;
      this.windOf = windOf;
      this.displayNameOf = displayNameOf;
    }

    public override AiType aiType => AiType.Llm;

    /// <summary>
    /// The nickname broadcast for this seat. Under anonymity this is a neutral
    /// label so nothing leaks through <c>PublicGameView</c> player nicknames or
    /// chat sender names into another agent's context (§9a). Humans still see
    /// real names via the match record, tracked out-of-band by seat.
    /// </summary>
    public override string nickname =>
        exposure.RevealOpponentIdentity && !string.IsNullOrWhiteSpace(model.DisplayName)
            ? model.DisplayName
            : $"Player {Seat + 1}";

    public override void OnEvent(EventBase ev) {
      EnsureInitialized();
      eventLog?.Record(ev);
    }

    /// <summary>
    /// Honors <c>exposure.chatToAgents</c> (§9). When off (default), this is a
    /// no-op: the rationale still lands in the replay via
    /// <c>Room.SendAgentChat</c>, but no other agent's chat ever enters this
    /// agent's prompt. When on, the incoming line is recorded with an
    /// anonymity-safe sender label for the next turn's context.
    /// </summary>
    public override void OnChat(int senderId, string text, string sticker) {
      if (!exposure.ChatToAgents) {
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

      // Out-of-turn reaction inquiries (chii/pon/daiminkan on someone's discard)
      // are noisy and low-value for an eval; take the safe default rather than
      // spending a full LLM decision on whether to call. Ron/tsumo and normal
      // turns still go to the model.
      var menu = LlmActionMenu.Build(playerInquiry, AiLocalization.LangEn);
      if (menu.Count == 0) {
        return InquiryResponse.Default(Seat);
      }

      EnsureMeta(view);

      var recent = eventLog?.Drain(out _) ?? Array.Empty<string>();
      List<ArenaChatLine> chats;
      lock (chatLock) {
        chats = pendingChats.ToList();
        pendingChats.Clear();
      }

      List<LlmMessage> baseTranscript;
      lock (convLock) {
        baseTranscript = transcript.ToList();
      }

      var decider = new ArenaLlmDecider(
          provider, model.Id, gameId, decision.MaxRetries,
          decision.MaxOutputTokens, TimeSpan.FromSeconds(decision.TimeoutSeconds),
          reasoning, usage);

      var seq = Interlocked.Increment(ref turnSeq);
      var result = decider.Decide(
          Seat, menu, playerInquiry, seq, baseTranscript,
          error => promptBuilder.BuildTurnMessage(
              view, menu, playerInquiry, recent, chats, error),
          remainingTimeout);

      // The decider appended the successful exchange to baseTranscript (its own
      // copy); mirror that into our shared transcript so the next turn has it.
      lock (convLock) {
        transcript.Clear();
        transcript.AddRange(baseTranscript);
      }

      EmitRationale(result.Rationale);
      return result.Response;
    }

    // ----- Initialization --------------------------------------------------

    private void EnsureInitialized() {
      if (initialized) {
        return;
      }
      lock (convLock) {
        if (initialized) {
          return;
        }
        var seat = Seat;
        labeler = new ArenaSeatLabeler(
            exposure.RevealOpponentIdentity, seat, ResolveWind, displayNameOf);
        promptBuilder = new ArenaPromptBuilder(labeler, AiLocalization.LangEn);
        // The event log labels seats with the same anonymity-safe labels.
        eventLog = new LlmEventLog(
            s => labeler.Label(s), AiLocalization.LangEn);
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
          if (transcript.Count == 0) {
            transcript.Add(LlmMessage.System(systemPrompt));
          }
          metaWritten = true;
        }
      }
      if (reasoning != null && ReasoningStore.IsValidGameId(gameId)) {
        try {
          reasoning.WriteMeta(new ReasoningMeta {
            GameId = gameId,
            Seat = Seat,
            ModelId = model.Id,
            Provider = model.Provider,
            Model = model.Model,
            SystemPrompt = systemPrompt,
            CreatedAt = DateTime.UtcNow.ToString("o"),
          });
        } catch {
          // Reasoning persistence must never break gameplay.
        }
      }
    }

    // ----- Chat / rationale ------------------------------------------------

    private void EmitRationale(string rationale) {
      if (string.IsNullOrWhiteSpace(rationale)) {
        return;
      }
      try {
        // Always record to the replay via the room's agent-chat tee, regardless
        // of exposure.chatToAgents (that gate only affects whether OTHER agents
        // fold this into their prompts — enforced in their OnChat).
        room.SendAgentChat(this, rationale, null);
      } catch (Exception e) {
        Logger.Warn($"Arena rationale broadcast failed: {e.Message}");
      }
    }

    // ----- Seat helpers ----------------------------------------------------

    private Wind ResolveWind(int seat) {
      if (windOf != null) {
        return windOf(seat);
      }
      // Fallback: derive from the live game if available.
      var game = room?.game;
      return game != null ? game.GetPlayer(seat).Wind : Wind.E;
    }

    private int SeatOfId(int senderId) {
      var agent = room?.players?.FirstOrDefault(p => p.id == senderId);
      return agent?.Seat ?? -1;
    }
  }
}
