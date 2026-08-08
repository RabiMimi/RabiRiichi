using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using TileSource = RabiRiichi.Generated.Core.TileSource;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Builds the decision prompt for an Arena playing agent: a static system
  /// prompt plus a per-turn user message combining the legal action menu, a
  /// game-state summary, and PRE-COMPUTED TOOL CONTEXT derived from a
  /// <see cref="PublicGameView"/> (ARENA_DESIGN.md §5/§6). Unlike the server's
  /// chat-only <c>LlmPromptBuilder</c>, this asks the model to actually pick a
  /// move.
  ///
  /// All opponent labels flow through an <see cref="ArenaSeatLabeler"/>, so
  /// under anonymity (§9a) the prompt/menu/tool-context carry only neutral seat
  /// labels — never a model id, display name, provider, or baseline-vs-LLM hint.
  /// Opponent data itself is keyed by seat (identity-neutral) via
  /// <see cref="PublicGameView"/>.
  /// </summary>
  public sealed class ArenaPromptBuilder {
    private readonly ArenaSeatLabeler labeler;
    private readonly string language;

    public ArenaPromptBuilder(
        ArenaSeatLabeler labeler, string language = AiLocalization.LangEn) {
      this.labeler = labeler;
      this.language = language;
    }

    /// <summary>
    /// The static system prompt (written once to the reasoning meta): role,
    /// tile notation, and the required <c>{ action, rationale }</c> output
    /// contract. Loaded from the embedded <c>Prompts/arena-system.md</c>
    /// template with <c>{{PLACEHOLDER}}</c> substitution, mirroring the server's
    /// <c>LlmPromptBuilder</c>. Carries no per-turn or opponent-identity info.
    /// </summary>
    public string BuildSystemPrompt(int selfSeat, PublicGameView view) {
      return LoadTemplate("arena-system", language)
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{TILE_NOTATION}}", TileNotationHint(language))
          .Replace("{{GAME_RULES}}", BuildGameRules(view))
          .TrimEnd();
    }

    /// <summary>
    /// Renders the static, human-visible game configuration (the same fields the
    /// web client's in-game info modal shows: length, min han, scoring/dora/
    /// riichi/ryuukyoku rules, and the enabled yaku). This is game-static, so it
    /// lives in the system prompt (written once) rather than per turn. It is
    /// rules the players legitimately know — never opponents' hidden info or any
    /// engine move recommendation.
    /// </summary>
    private string BuildGameRules(PublicGameView view) {
      var sb = new StringBuilder();
      var length = view.TotalRound <= 1 ? "East-only (tonpuusen)"
          : view.TotalRound == 2 ? "East + South (hanchan)"
          : $"{view.TotalRound} wind rounds";
      sb.Append("  - Length: ").Append(length).AppendLine(".");
      sb.Append("  - Players: ").Append(view.PlayerCount).AppendLine(".");
      sb.Append("  - Minimum han to win: ").Append(view.MinHan).AppendLine(".");
      sb.Append("  - Starting points: ").Append(view.InitialPoints)
        .Append("; target to finish: ").Append(view.FinishPoints).AppendLine(".");
      sb.Append("  - Open tanyao (kuitan): ")
        .AppendLine(view.AllowsOpenTanyao ? "allowed" : "not allowed");
      sb.Append("  - Dora rule: ").AppendLine(view.DoraOptions);
      sb.Append("  - Scoring rule: ").AppendLine(view.ScoringOptions);
      sb.Append("  - Riichi rule: ").AppendLine(view.RiichiPolicy);
      sb.Append("  - Kuikae rule: ").AppendLine(view.KuikaePolicy);
      sb.Append("  - Renchan rule: ").AppendLine(view.RenchanPolicy);
      sb.Append("  - Ryuukyoku trigger: ").AppendLine(view.RyuukyokuTriggers);

      var yaku = view.AllowedYakus;
      if (yaku != null && yaku.Count > 0) {
        sb.Append("  - Enabled yaku (").Append(yaku.Count).Append("): ")
          .AppendLine(string.Join(", ", yaku));
      }
      return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Loads an embedded prompt template by base name, preferring a
    /// language-specific variant (<c>{name}_{lang}.md</c>) and falling back to
    /// the default (<c>{name}.md</c>). Mirrors
    /// <c>LlmPromptBuilder.LoadTemplate</c>.
    /// </summary>
    private static string LoadTemplate(string baseName, string language) {
      var lang = AiLocalization.NormalizeLanguage(language);
      var assembly = typeof(ArenaPromptBuilder).Assembly;
      string resourceName = null;

      if (lang != AiLocalization.LangEn) {
        var localized = $".Prompts.{baseName}_{lang}.md";
        resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(localized, StringComparison.Ordinal));
      }
      resourceName ??= assembly.GetManifestResourceNames()
          .SingleOrDefault(name =>
              name.EndsWith($".Prompts.{baseName}.md", StringComparison.Ordinal));

      if (resourceName == null) {
        throw new InvalidOperationException(
            $"Arena prompt template not found: {baseName} (language {language}).");
      }

      using var stream = assembly.GetManifestResourceStream(resourceName);
      using var reader = new StreamReader(stream!);
      return reader.ReadToEnd();
    }

    private static string TileNotationHint(string language) {
      return "m = manzu (characters), p = pinzu (dots), s = souzu (bamboo), " +
          "z = honors. 1z=East, 2z=South, 3z=West, 4z=North, 5z=White dragon, " +
          "6z=Green dragon, 7z=Red dragon. A leading 0 marks a red five (e.g. 0p).";
    }

    /// <summary>
    /// Builds the per-turn user message: recent events (optional), any folded-in
    /// chat (only supplied when <c>exposure.chatToAgents</c> is on), game-state
    /// summary, tool context, and the numbered legal-action menu. This is exactly
    /// the <c>promptDelta</c> persisted for the turn (§8) — it never includes the
    /// system prompt or prior turns.
    /// </summary>
    public string BuildTurnMessage(
        PublicGameView view,
        IReadOnlyList<LlmChoice> menu,
        IReadOnlyList<string> recentEvents = null,
        IReadOnlyList<ArenaChatLine> chats = null,
        string validationError = null) {
      var sb = new StringBuilder();

      if (!string.IsNullOrEmpty(validationError)) {
        sb.AppendLine(
            "Your previous answer was REJECTED as illegal: " + validationError);
        sb.AppendLine(
            "Choose again using ONLY an id from the menu below.");
        sb.AppendLine();
      }

      if (recentEvents != null && recentEvents.Count > 0) {
        sb.AppendLine("Recent events:");
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
      }

      AppendChats(sb, chats);
      AppendGameState(sb, view);
      AppendToolContext(sb, view);
      AppendMenu(sb, menu);

      sb.AppendLine();
      sb.AppendLine(
          "Return your choice as {\"action\": <id>, \"rationale\": \"<why>\"}.");
      return sb.ToString().TrimEnd();
    }

    // ----- Sections --------------------------------------------------------

    private void AppendGameState(StringBuilder sb, PublicGameView view) {
      sb.AppendLine(
          $"Round: {WindLabel(view.RoundWind)} {view.Round % view.PlayerCount + 1}, " +
          $"honba {view.Honba}, riichi sticks {view.RiichiStick}. " +
          $"Wall tiles left: {view.WallRemaining}.");
      sb.Append("Your seat wind: ").Append(WindLabel(view.SelfWind))
        .AppendLine(view.SelfIsDealer ? " (you are the dealer)." : ".");

      var doraIndicators = view.RevealedDoraIndicators;
      sb.Append("Dora indicator(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(doraIndicators, language));

      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));

      var selfRiver = ActiveRiver(view.DiscardsOf(view.Seat));
      sb.Append("Your discards (river): ")
        .AppendLine(selfRiver.Count == 0 ? "none" : TileNotation.Group(selfRiver, language));
    }

    private void AppendToolContext(StringBuilder sb, PublicGameView view) {
      sb.AppendLine("== Pre-computed analysis (tools) ==");

      // Self summary.
      var selfShanten = SelfShanten(view);
      sb.Append("Self: ");
      sb.Append(selfShanten == int.MaxValue
          ? "shanten unavailable"
          : selfShanten < 0 ? "complete hand"
          : selfShanten == 0 ? "TENPAI"
          : $"{selfShanten}-shanten");
      if (view.SelfRiichi) sb.Append(", in riichi");
      if (view.SelfMenzen) sb.Append(", concealed"); else sb.Append(", open");
      if (view.SelfFuriten) sb.Append(", FURITEN");
      sb.AppendLine(".");

      var tenpai = view.SelfTenpaiInfos();
      if (tenpai.Count > 0) {
        sb.Append("  Waits (before the current draw): ");
        sb.AppendLine(string.Join(", ", tenpai.Select(info => {
          var value = info.yakuman > 0
              ? $"{info.yakuman} yakuman"
              : $"{info.han} han ({info.yaku} yaku han), {info.fu} fu";
          return $"{TileNotation.One(info.winningTile, language)} " +
              $"[{value}; {view.UnseenCount(info.winningTile)} live]";
        })));
      }

      // Opponents, strictly by seat label (identity-neutral).
      foreach (var seat in view.OpponentSeats.OrderBy(s => s)) {
        sb.Append("  ").Append(labeler.Label(seat)).Append(": ")
          .Append($"{view.PointsOf(seat)} pts, {WindLabel(view.WindOf(seat))} wind, jun {view.JunOf(seat)}");
        var flags = new List<string>();
        if (view.IsDealer(seat)) flags.Add("dealer");
        if (view.IsRiichi(seat)) flags.Add("riichi");
        if (view.IsIppatsu(seat)) flags.Add("ippatsu");
        if (flags.Count > 0) sb.Append(", ").Append(string.Join(", ", flags));
        sb.AppendLine(".");

        var discards = ActiveRiver(view.DiscardsOf(seat));
        if (discards.Count > 0) {
          sb.Append("    discards: ")
            .AppendLine(TileNotation.Group(discards, language));
        }
        var called = view.CalledOf(seat);
        if (called.Count > 0) {
          sb.Append("    melds: ")
            .AppendLine(string.Join(" ", called.Select(m => DescribeMeld(m, view))));
        }
      }
    }

    private void AppendMenu(StringBuilder sb, IReadOnlyList<LlmChoice> menu) {
      sb.AppendLine("== Legal actions (choose one by id) ==");
      foreach (var choice in menu) {
        sb.Append("  [").Append(choice.Id).Append("] ")
          .AppendLine(choice.Description);
      }
    }

    private void AppendChats(StringBuilder sb, IReadOnlyList<ArenaChatLine> chats) {
      if (chats == null || chats.Count == 0) {
        return;
      }
      sb.AppendLine("Table chat since your last turn:");
      foreach (var chat in chats) {
        sb.Append("  - ").Append(chat.Sender).Append(": ")
          .AppendLine(chat.Text);
      }
    }

    // ----- Helpers ---------------------------------------------------------

    /// <summary>
    /// A seat's LIVE discard river — the tiles still sitting in front of them.
    /// The engine leaves a claimed tile in the discarder's river (a pon/chi/kan
    /// only reassigns the tile's owner + source), so we drop any tile whose source
    /// is no longer a plain discard. That called tile is instead shown inside the
    /// caller's meld, annotated with the discarder — closer to a real table, where
    /// a called tile is physically taken out of the river and rotated in the meld.
    /// </summary>
    private static IReadOnlyList<GameTile> ActiveRiver(IReadOnlyList<GameTile> discards) =>
        discards.Where(t => t.source == TileSource.Discard).ToList();

    /// <summary>
    /// Renders a called meld like a real table would: the meld type, its tiles
    /// (claimed tile marked by <see cref="TileNotation.Meld"/>), and — for an open
    /// meld — WHICH tile was claimed and from WHICH player ("you" when it was the
    /// viewer's own discard). Naming the claimed tile matters for chii, where the
    /// meld notation alone (e.g. <c>-456m</c>) doesn't say whether 4m, 5m, or 6m
    /// came off the discarder's river. Concealed kans (ankan) carry no source.
    /// </summary>
    private string DescribeMeld(MenLike meld, PublicGameView view) {
      var body = $"{MeldTypeLabel(meld)} {TileNotation.Meld(meld, language)}";
      var claimed = meld.FirstOrDefault(t => !t.IsTsumo && t.discardInfo != null);
      return claimed == null
          ? body
          : $"{body} ({TileNotation.One(claimed, language)} from " +
            $"{SourceLabel(claimed.discardInfo.from, view)})";
    }

    private static string MeldTypeLabel(MenLike meld) => meld switch {
      Kan k => k.KanSource == TileSource.Ankan ? "ankan"
          : k.KanSource == TileSource.Kakan ? "added-kan" : "kan",
      Kou => "pon",
      Shun => "chii",
      Jantou => "pair",
      _ => "meld",
    };

    /// <summary>Anonymity-safe label for the player a called tile came from.</summary>
    private string SourceLabel(int seat, PublicGameView view) =>
        seat == view.Seat ? "you" : labeler.Label(seat);

    private static int SelfShanten(PublicGameView view) {
      var hand = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        hand.Add(view.SelfHand.pendingTile);
      }
      return view.ShantenOf(hand);
    }

    private string DescribeSelfHand(PublicGameView view) {
      var hand = view.SelfHand;
      var sb = new StringBuilder(TileNotation.Group(hand.freeTiles, language));
      if (hand.pendingTile != null) {
        sb.Append(" + drew ").Append(TileNotation.One(hand.pendingTile, language));
      }
      if (hand.called.Count > 0) {
        sb.Append(" | melds: ").Append(string.Join(" ",
            hand.called.Select(m => DescribeMeld(m, view))));
      }
      return sb.ToString();
    }

    private static string WindLabel(Wind wind) => wind switch {
      Wind.E => "East",
      Wind.S => "South",
      Wind.W => "West",
      Wind.N => "North",
      _ => wind.ToString(),
    };
  }

  /// <summary>
  /// One incoming chat line already resolved to an anonymity-safe sender label.
  /// Only used when <c>exposure.chatToAgents</c> is enabled (§9).
  /// </summary>
  public sealed record ArenaChatLine(string Sender, string Text);
}
