using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Generated.Rpc;
using System.Text;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// One quoted line of table chat. <see cref="Role"/> is carried alongside the
  /// name so each turn's prompt can say what the speaker IS, not just what they
  /// are called - a persona that treats human players differently from bots must
  /// not have to remember a name-to-role mapping from a system prompt sent many
  /// turns ago.
  /// </summary>
  public sealed record LlmChatEntry(
      string Sender, string Text, string Sticker, LlmSeatRole? Role = null);
  public enum LlmSeatRole { Human, Llm, OtherAi }

  /// <summary>Builds prompts from an embedded markdown persona template.</summary>
  public sealed class LlmPromptBuilder(
      LlmSettings settings,
      IReadOnlyDictionary<int, string> seatNames,
      IReadOnlyDictionary<int, LlmSeatRole> seatRoles = null) {
    private const int DetailedChatLimit = 10;
    private const int ChatTextLimit = 256;
    private readonly LlmSettings settings = settings;
    private readonly IReadOnlyDictionary<int, string> seatNames = seatNames;
    private readonly IReadOnlyDictionary<int, LlmSeatRole> seatRoles = seatRoles;

    private string NameOf(int seat) =>
        seatNames.TryGetValue(seat, out var n) ? n : $"P{seat}";

    private string CurrentPlayerName(PublicGameView view) =>
        view.CurrentPlayer >= 0 && view.CurrentPlayer < view.PlayerCount
            ? NameOf(view.CurrentPlayer)
            : "not set";

    public static bool ShouldSendConsecutiveChatReminder(int consecutiveChatTurns) =>
        consecutiveChatTurns >= 3;

    public string BuildSystemPrompt(int selfSeat, PublicGameView view = null) {
      var opponents = string.Join("\n", seatNames
          .Where(kv => kv.Key != selfSeat)
          .OrderBy(kv => kv.Key)
          .Select(kv => $"- seat {kv.Key}: {kv.Value}{RoleLabel(kv.Key)}"));
      var prompt = LoadTemplate(settings.PromptTemplate, settings.Language)
          .Replace("{{SELF_NAME}}", NameOf(selfSeat))
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{LANGUAGE}}", LanguageLabel(settings.Language))
          .Replace("{{TILE_NOTATION}}", TileNotationHint(settings.Language))
          .Replace("{{KAN_GUIDE}}", KanGuide(settings.Language))
          .Replace("{{OPPONENTS}}", opponents)
          .Replace("{{STICKER_MOODS}}", string.Join(", ", StickerRegistry.Moods));
      prompt += "\n\n" + ReferenceMoveRules;
      return view == null ? prompt : prompt + "\n\n" + BuildGameConfiguration(view);
    }

    /// <summary>
    /// The standing rules for how to read the per-turn reference move.
    ///
    /// This lives in the system prompt, which is sent once and is never dropped
    /// when the transcript is trimmed, so it stays in context for the whole game
    /// at no per-turn cost. The turn prompt then only needs a one-line marker.
    ///
    /// The emphatic "nobody said this to you" framing is load-bearing: a persona
    /// that is told to obey human players will otherwise read the reference move
    /// as an instruction from its owner and start thanking people for it.
    /// </summary>
    public const string ReferenceMoveRules =
        "== How the reference move works (standing rules) ==\n" +
        "Every turn prompt lists your legal actions with numeric ids and marks one of them as the reference move.\n" +
        "The reference move is your OWN silent read of the position - your trained instinct for tile efficiency, hand value, the table's minimum-han requirement and safety. Nobody says it to you. It is never a message, a request, or an instruction from any player: no one at the table can see it, no one is waiting for you to acknowledge it, and nobody has asked you to play it. Never thank anyone for it, never answer it as if it had been spoken to you, and never describe your move as something you were told to make.\n" +
        "The only things players actually say to you are the quoted lines under \"Table chat\" in the turn prompt. When that section is missing, nobody has spoken to you and there is nothing to reply to or comply with.\n" +
        "The reference move is a suggestion, not an order: pick a different id whenever you can articulate a concrete reason (defence, a read on an opponent, hand value, placement, or something a player actually said in the table chat). If you are unsure, follow the reference move - in most positions it is correct.\n" +
        "Your answer MUST set \"action\" to one of the ids listed that turn. An invalid or missing id is discarded and the reference move is played instead.";

    public string BuildGameConfiguration(PublicGameView view) {
      var matchLength = view.TotalRound switch {
        1 => "East-only",
        2 => "East-South",
        _ => $"{view.TotalRound} wind rounds",
      };
      var sb = new StringBuilder("== Game configuration (sent once) ==\n");
      sb.AppendLine($"Players: {view.PlayerCount}; match length: {matchLength}; " +
          $"minimum yaku han to win: {view.MinHan} (bonus/dora han do not count).");
      sb.AppendLine($"Starting points: {view.InitialPoints}; target points: " +
          $"{view.FinishPoints}; riichi deposit: {view.RiichiPoints}; " +
          $"honba value: {view.HonbaPoints}.");
      sb.AppendLine($"Tiles in play: {view.InitialTileCount}; action timeout: " +
          $"{view.GameplayActionTimeout:0.##} seconds.");
      sb.Append("Initial tile composition: ")
        .AppendLine(TileNotation.Group(view.InitialTiles, settings.Language));
      sb.AppendLine("Rule options: " +
          $"renchan={view.RenchanPolicy}; endGame={view.EndGamePolicy}; " +
          $"kuikae={view.KuikaePolicy}; riichi={view.RiichiPolicy}; " +
          $"dora={view.DoraOptions}; agari={view.AgariOptions}; " +
          $"scoring={view.ScoringOptions}; abortiveDraws={view.RyuukyokuTriggers}.");
      sb.Append("Allowed yaku: ").AppendLine(view.AllowedYakus.Count == 0
          ? "none listed"
          : string.Join(", ", view.AllowedYakus));
      return sb.ToString().TrimEnd();
    }

    private string RoleLabel(int seat) {
      if (seatRoles == null || !seatRoles.TryGetValue(seat, out var role)) return "";
      return settings.PromptTemplate switch {
        LlmPromptTemplate.Mesugaki => MesugakiRoleLabel(role),
        LlmPromptTemplate.BunnyPet => BunnyPetRoleLabel(role),
        _ => NeutralRoleLabel(role),
      };
    }

    private string NeutralRoleLabel(LlmSeatRole role) {
      return (role, AiLocalization.NormalizeLanguage(settings.Language)) switch {
        (LlmSeatRole.Human, AiLocalization.LangJa) => "（人間プレイヤー）",
        (LlmSeatRole.Llm, AiLocalization.LangJa) => "（LLMプレイヤー）",
        (LlmSeatRole.OtherAi, AiLocalization.LangJa) => "（AIプレイヤー）",
        (LlmSeatRole.Human, AiLocalization.LangZhs) => "（人类玩家）",
        (LlmSeatRole.Llm, AiLocalization.LangZhs) => "（LLM玩家）",
        (LlmSeatRole.OtherAi, AiLocalization.LangZhs) => "（AI玩家）",
        (LlmSeatRole.Human, _) => " (human player)",
        (LlmSeatRole.Llm, _) => " (LLM player)",
        (LlmSeatRole.OtherAi, _) => " (AI player)",
        _ => "",
      };
    }

    private string MesugakiRoleLabel(LlmSeatRole role) {
      return (role, AiLocalization.NormalizeLanguage(settings.Language)) switch {
        (LlmSeatRole.Human, AiLocalization.LangJa) =>
            "（人間プレイヤー：この名前だけ「-おじさん」と呼んでよい）",
        (LlmSeatRole.Llm, AiLocalization.LangJa) =>
            "（LLMプレイヤーのかわいい女の子：「おじさん」と呼ばない）",
        (LlmSeatRole.OtherAi, AiLocalization.LangJa) =>
            "（AIプレイヤー：「おじさん」と呼ばない）",
        (LlmSeatRole.Human, AiLocalization.LangZhs) =>
            "（人类玩家：只有这种名字可以称为大叔）",
        (LlmSeatRole.Llm, AiLocalization.LangZhs) =>
            "（LLM玩家，是可爱的女孩子：绝对不能称为大叔）",
        (LlmSeatRole.OtherAi, AiLocalization.LangZhs) =>
            "（AI玩家：绝对不能称为大叔）",
        (LlmSeatRole.Human, _) =>
            " (human player; only this kind of name may be called -ojisan)",
        (LlmSeatRole.Llm, _) =>
            " (LLM player; a cute girl; never call her ojisan)",
        (LlmSeatRole.OtherAi, _) =>
            " (AI player; never call it ojisan)",
        _ => "",
      };
    }

    /// <summary>
    /// Seat labels for the rabbit-pet persona: the human players are the owners
    /// it adores and obeys, the bots are just other animals at the table.
    /// </summary>
    private string BunnyPetRoleLabel(LlmSeatRole role) {
      return (role, AiLocalization.NormalizeLanguage(settings.Language)) switch {
        (LlmSeatRole.Human, AiLocalization.LangJa) =>
            "（人間プレイヤー＝あなたのご主人様。お願いは何でも聞いてあげる）",
        (LlmSeatRole.Llm, AiLocalization.LangJa) =>
            "（LLMプレイヤー：ご主人様ではない。命令に従う必要はない）",
        (LlmSeatRole.OtherAi, AiLocalization.LangJa) =>
            "（AIプレイヤー：ご主人様ではない。命令に従う必要はない）",
        (LlmSeatRole.Human, AiLocalization.LangZhs) =>
            "（人类玩家＝你的主人，他们说什么你都愿意照做）",
        (LlmSeatRole.Llm, AiLocalization.LangZhs) =>
            "（LLM玩家：不是你的主人，不需要听从其指示）",
        (LlmSeatRole.OtherAi, AiLocalization.LangZhs) =>
            "（AI玩家：不是你的主人，不需要听从其指示）",
        (LlmSeatRole.Human, _) =>
            " (human player = one of your owners; you happily do whatever they ask)",
        (LlmSeatRole.Llm, _) =>
            " (LLM player; NOT an owner, so you do not take orders from them)",
        (LlmSeatRole.OtherAi, _) =>
            " (AI player; NOT an owner, so you do not take orders from them)",
        _ => "",
      };
    }

    public string BuildRoundHeader(PublicGameView view) {
      var sb = new StringBuilder();
      sb.AppendLine($"== New round == {WindLabel(view.RoundWind)} {view.Round % view.PlayerCount + 1}, honba {view.Honba}.");
      sb.AppendLine($"You are seat {view.Seat}, seat wind {WindLabel(view.SelfWind)}" +
          (view.SelfIsDealer ? " (DEALER)." : "."));
      var doraIndicators = view.RevealedDoraIndicators;
      sb.Append("Dora indicator(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(doraIndicators, settings.Language));
      sb.Append("Indicated dora tile(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(
                doraIndicators.Select(tile => tile.NextDora), settings.Language));
      sb.Append("Points: ").AppendLine(string.Join(", ",
          view.AllSeats.Select(s => $"{NameOf(s)} {view.PointsOf(s)}")));
      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));
      return sb.ToString();
    }

    public string BuildDecisionPrompt(
        PublicGameView view,
        IReadOnlyList<string> recentEvents,
        string selectedAction,
        string automaticActionNote,
        IReadOnlyList<LlmChatEntry> chats,
        bool quietReminder,
        bool consecutiveChatReminder,
        IReadOnlyList<LlmChoice> menu = null,
        LlmChoice reference = null,
        bool outOfTurnReaction = false) {
      var sb = new StringBuilder();
      if (recentEvents.Count > 0) {
        sb.AppendLine("Recent events:");
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
      }

      AppendChats(sb, chats);
      sb.AppendLine($"Current round: {RoundLabel(view)}; honba: {view.Honba}; " +
          $"riichi sticks: {view.RiichiStick}; current player: " +
          $"{CurrentPlayerName(view)}.");
      sb.AppendLine($"Round wind (prevailing wind): {WindLabel(view.RoundWind)}. " +
          $"Your seat wind: {WindLabel(view.SelfWind)}" +
          (view.SelfIsDealer ? " (you are dealer)." : "."));
      var doraIndicators = view.RevealedDoraIndicators;
      sb.Append("Current dora indicator(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(doraIndicators, settings.Language));
      sb.Append("Current indicated dora tile(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(
                doraIndicators.Select(tile => tile.NextDora), settings.Language));
      sb.Append("Players: ").AppendLine(string.Join("; ", view.AllSeats.Select(seat => {
        var flags = new List<string>();
        if (view.IsDealer(seat)) flags.Add("dealer");
        if (view.IsRiichi(seat)) flags.Add("riichi");
        if (view.IsIppatsu(seat)) flags.Add("ippatsu");
        var suffix = flags.Count == 0 ? "" : $", {string.Join(", ", flags)}";
        return $"{NameOf(seat)}: {view.PointsOf(seat)} points, " +
            $"{WindLabel(view.WindOf(seat))} seat wind, jun {view.JunOf(seat)}{suffix}";
      })));
      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));
      sb.Append("Hand status: ").AppendLine(DescribeSelfHandStatus(view));
      if (view.SelfFuriten) sb.AppendLine("You are currently FURITEN.");
      sb.Append("Wall tiles left: ").Append(view.WallRemaining);
      var riichiSeats = view.OpponentSeats.Where(view.IsRiichi).Select(NameOf).ToList();
      if (riichiSeats.Count > 0) {
        sb.Append(". In riichi: ").Append(string.Join(", ", riichiSeats));
      }
      sb.AppendLine(".");

      foreach (var s in view.OpponentSeats) {
        var discards = view.DiscardsOf(s);
        if (discards.Count > 0) {
          sb.Append($"  {NameOf(s)} discards: ")
            .AppendLine(TileNotation.Group(discards, settings.Language));
        }
        var called = view.CalledOf(s);
        if (called.Count > 0) {
          sb.Append($"  {NameOf(s)} melds: ")
            .AppendLine(string.Join(" ",
                called.Select(meld => TileNotation.Meld(meld, settings.Language))));
        }
      }

      if (menu is { Count: > 0 } && outOfTurnReaction) {
        sb.AppendLine("This is an out-of-turn reaction to somebody else's discard, not your own turn. Decide quickly and stay silent unless the moment really deserves a word.");
      }
      if (menu is { Count: > 0 }) {
        sb.AppendLine("Your legal actions this turn (choose exactly one by its id):");
        sb.AppendLine(LlmActionMenu.Render(menu, reference));
        if (reference != null) {
          // The standing rules for this live in the system prompt; repeating
          // them here would cost ~275 tokens every single turn. The parenthetical
          // is the minimum needed to keep the "this is not an instruction" read
          // from drifting.
          sb.Append("Reference move: [").Append(reference.Id).Append("] ")
            .Append(reference.Description)
            .AppendLine(" (your own read; nobody said this to you)");
        }
      } else {
        sb.Append("You decided to: ").AppendLine(selectedAction);
        sb.AppendLine("The action above is context, not a request to chat. Default to silence by setting both say and sticker to null. Only add a message when the situation itself is genuinely interesting and worth commenting on. If you do comment, treat the action as your own decision and never mention that it was selected or supplied externally.");
      }
      if (!string.IsNullOrEmpty(automaticActionNote)) {
        sb.AppendLine(automaticActionNote);
      }
      if (quietReminder) {
        sb.AppendLine("You have been quiet for at least 5 turns. Please chat or use a sticker within the next few turns when it feels natural.");
      }
      if (consecutiveChatReminder) {
        sb.AppendLine("You have already chatted on 3 or more consecutive turns. Return say=null and sticker=null this turn. The only exception is an exceptional game event or a message directly addressed to you that genuinely requires a response.");
      }
      // Restating the exact schema every turn is worth its ~20 tokens: it is what
      // keeps malformed answers rare. The prose around it is not, and lives in
      // the system prompt instead.
      sb.AppendLine(menu is { Count: > 0 }
          ? "Reply JSON only: {\"action\": <id>, \"say\": <text or null>, \"sticker\": <mood or null>}"
          : "Reply JSON only: {\"say\": <text or null>, \"sticker\": <mood or null>}");
      return sb.ToString();
    }

    public string BuildEndGamePrompt(
        PublicGameView view,
        IReadOnlyList<string> recentEvents,
        IReadOnlyList<LlmChatEntry> chats,
        IReadOnlyList<long> endGamePoints = null) {
      var sb = new StringBuilder();
      if (recentEvents != null && recentEvents.Count > 0) {
        sb.AppendLine("Recent events:");
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
      }

      AppendChats(sb, chats);

      sb.AppendLine("== GAME OVER - FINAL RANKINGS ==");
      var points = new Dictionary<int, long>();
      for (int s = 0; s < view.PlayerCount; s++) {
        points[s] = (endGamePoints != null && s < endGamePoints.Count)
            ? endGamePoints[s]
            : view.PointsOf(s);
      }
      var ranked = points.OrderByDescending(kv => kv.Value).ToList();
      for (int r = 0; r < ranked.Count; r++) {
        var seat = ranked[r].Key;
        var p = ranked[r].Value;
        var isSelf = (seat == view.Seat) ? " (YOU)" : "";
        sb.AppendLine($"  Rank #{r + 1}: {NameOf(seat)}{isSelf} with {p} points");
      }

      var selfRank = ranked.FindIndex(kv => kv.Key == view.Seat) + 1;
      sb.AppendLine($"The game is now complete. You finished in rank #{selfRank} out of {view.PlayerCount}.");
      sb.AppendLine("Provide your final end-of-game comment or reaction to the table in persona (commenting on your final rank and the game outcome).");
      sb.AppendLine("Return a single JSON object: {\"say\": \"<short final chat or null>\", \"sticker\": \"<mood or null>\"}.");

      return sb.ToString();
    }

    /// <summary> e.g. "East 1". </summary>
    private string RoundLabel(PublicGameView view) =>
        $"{WindLabel(view.RoundWind)} {view.Round % view.PlayerCount + 1}";

    /// <summary>
    /// What to KEEP in the transcript in place of a turn prompt once that turn
    /// is over.
    ///
    /// A turn prompt is dominated by a full snapshot of the position - every
    /// river, our hand and waits, dora, the whole legal action list. One turn
    /// later all of it is stale, and the next prompt restates it anyway, so
    /// carrying the original forward pays full price for information that is
    /// both redundant and wrong. What history is actually for is continuity:
    /// what the model said, and what people said to it. The assistant replies
    /// are kept verbatim; this is the matching user side.
    /// </summary>
    public string BuildHistorySummary(PublicGameView view, IReadOnlyList<LlmChatEntry> chats) {
      var sb = new StringBuilder();
      sb.Append("(Earlier turn - ").Append(RoundLabel(view))
        .Append(", your turn ").Append(view.SelfJun)
        .AppendLine(". The position from that turn is omitted; only the newest turn's position is current.)");
      if (chats.Count > 0) {
        AppendChats(sb, chats);
      }
      return sb.ToString().TrimEnd();
    }

    private static void AppendChats(StringBuilder sb, IReadOnlyList<LlmChatEntry> chats) {
      if (chats.Count == 0) {
        return;
      }
      sb.AppendLine("Table chat since you last interacted:");
      sb.AppendLine("These are the only things anyone actually said to you. Do not repeat their exact wording, present it as your own message, or impersonate its sender.");
      foreach (var chat in chats.Take(DetailedChatLimit)) {
        sb.Append("  - ").Append(chat.Sender).Append(ChatRoleTag(chat.Role)).Append(": ");
        if (!string.IsNullOrEmpty(chat.Text)) {
          sb.Append(TrimChat(chat.Text));
        }
        if (!string.IsNullOrEmpty(chat.Sticker)) {
          if (!string.IsNullOrEmpty(chat.Text))
            sb.Append(' ');
          sb.Append("<sticker: ").Append(chat.Sticker).Append('>');
        }
        sb.AppendLine();
      }
      if (chats.Count > DetailedChatLimit) {
        sb.Append("They then chatted in this order (details omitted): ")
          .Append(string.Join(", ", chats.Skip(DetailedChatLimit).Select(c => c.Sender)))
          .AppendLine(".");
      }
    }

    /// <summary>
    /// What the speaker of a chat line is. Deliberately blunt and repeated on
    /// every line so a persona with role-dependent behaviour cannot drift.
    /// </summary>
    private static string ChatRoleTag(LlmSeatRole? role) => role switch {
      LlmSeatRole.Human => " (human player)",
      LlmSeatRole.Llm => " (LLM player, not a human)",
      LlmSeatRole.OtherAi => " (AI player, not a human)",
      _ => "",
    };

    private static string TrimChat(string text) => text.Length <= ChatTextLimit
        ? text
        : text[..ChatTextLimit] + "... <trimmed due to length>";

    private static string LoadTemplate(LlmPromptTemplate template, string language) {
      var baseName = template switch {
        LlmPromptTemplate.CuteJk => "cute-jk",
        LlmPromptTemplate.Mesugaki => "mesugaki",
        LlmPromptTemplate.BunnyPet => "bunny-pet",
        _ => throw new ArgumentOutOfRangeException(nameof(template)),
      };
      var lang = AiLocalization.NormalizeLanguage(language);
      
      var assembly = typeof(LlmPromptBuilder).Assembly;
      string resourceName = null;
      string suffix = null;

      if (lang != AiLocalization.LangEn) {
        suffix = $".Agents.Llm.Prompts.{baseName}_{lang}.md";
        resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
      }

      if (resourceName == null) {
        suffix = $".Agents.Llm.Prompts.{baseName}.md";
        resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
      }

      if (resourceName == null) {
        throw new InvalidOperationException($"LLM prompt resource not found for template {template} and language {language} (suffix: {suffix})");
      }

      using var stream = assembly.GetManifestResourceStream(resourceName);
      using var reader = new StreamReader(stream!);
      return reader.ReadToEnd();
    }

    private string DescribeSelfHand(PublicGameView view) {
      var hand = view.SelfHand;
      var sb = new StringBuilder(TileNotation.Group(hand.freeTiles, settings.Language));
      if (hand.pendingTile != null)
        sb.Append(" + drew ").Append(TileNotation.One(hand.pendingTile, settings.Language));
      if (hand.called.Count > 0) {
        sb.Append(" | melds: ").Append(string.Join(" ",
            hand.called.Select(meld => TileNotation.Meld(meld, settings.Language))));
      }
      return sb.ToString();
    }

    private string DescribeSelfHandStatus(PublicGameView view) {
      var shanten = view.ShantenOf(view.SelfHand.freeTiles);
      if (shanten == int.MaxValue) return "unavailable";
      if (shanten < 0) return "complete winning shape";
      if (shanten > 0) return $"{shanten}-shanten (not in tenpai)";

      var waits = view.SelfTenpaiInfos();
      if (waits.Count == 0) return "TENPAI (wait/value details unavailable)";
      return "TENPAI; waits and guaranteed-minimum ron estimates: " +
          string.Join(", ", waits.Select(info => {
            var value = info.yakuman > 0
                ? $"{info.yakuman} yakuman"
                : $"{info.han} han ({info.yaku} yaku han), {info.fu} fu";
            return $"{TileNotation.One(info.winningTile, settings.Language)} " +
                $"[{value}; {view.UnseenCount(info.winningTile)} unseen]";
          }));
    }

    private static string WindLabel(Wind wind) => wind switch {
      Wind.E => "East",
      Wind.S => "South",
      Wind.W => "West",
      Wind.N => "North",
      _ => wind.ToString(),
    };

    private static string LanguageLabel(string language) => language switch {
      AiLocalization.LangZhs => "Simplified Chinese (简体中文)",
      AiLocalization.LangJa => "Japanese (日本語)",
      _ => "English",
    };


    private static string TileNotationHint(string language) => language switch {
      AiLocalization.LangJa =>
          "牌表記: m=萬子、p=筒子、s=索子、z=字牌。0は赤ドラ（例: 0p）、*は万能牌（任意の牌の代用となるワイルドカード）。" +
          "1z=東、2z=南、3z=西、4z=北、5z=白、6z=發、7z=中。" +
          "風牌は1z〜4zだけで、5z〜7zは三元牌です。自風は自分の席の風、場風はその局の共通の風で、別々に示されます。同じ風なら両方に該当します。",
      AiLocalization.LangZhs =>
          "牌张记法：m=万子，p=筒子，s=索子，z=字牌。0表示赤宝牌（如0p），*表示万能牌（可充当任意牌的百搭牌）。" +
          "1z=东，2z=南，3z=西，4z=北，5z=白，6z=发，7z=中。" +
          "只有1z至4z是风牌，5z至7z是三元牌。自风是你座位的风，场风是本局共同的风，两者会分别标明；相同时该风同时属于自风和场风。",
      _ =>
          "Tile notation: m = characters/manzu, p = dots/pinzu, s = bamboo/souzu, and z = honors. " +
          "0 marks a red five (e.g. 0p). * marks a universal wildcard (can stand for any tile). " +
          "1z = East, 2z = South, 3z = West, 4z = North, 5z = white dragon, 6z = green dragon, 7z = red dragon. " +
          "Only 1z-4z are winds; 5z-7z are dragons. Your seat wind and the prevailing round wind are listed separately; if they are the same, that wind counts as both.",
    };

    private static string KanGuide(string language) {
      var prefix = AiLocalization.NormalizeLanguage(language) switch {
        AiLocalization.LangJa => "槓の3種類を混同しないでください：",
        AiLocalization.LangZhs => "不要混淆以下三种杠：",
        _ => "Never confuse the three kan types:",
      };
      return $"{prefix}\n" +
          $"- {LlmKanNotation.Describe(TileSource.Ankan, language)}\n" +
          $"- {LlmKanNotation.Describe(TileSource.Kakan, language)}\n" +
          $"- {LlmKanNotation.Describe(TileSource.Daiminkan, language)}";
    }
  }
}
