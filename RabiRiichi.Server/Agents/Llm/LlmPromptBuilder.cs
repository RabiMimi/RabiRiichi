using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Generated.Rpc;
using System.Text;

namespace RabiRiichi.Server.Agents.Llm {
  public sealed record LlmChatEntry(string Sender, string Text, string Sticker);
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
        consecutiveChatTurns >= 2;

    public string BuildSystemPrompt(int selfSeat, PublicGameView view = null) {
      var opponents = string.Join("\n", seatNames
          .Where(kv => kv.Key != selfSeat)
          .OrderBy(kv => kv.Key)
          .Select(kv => $"- seat {kv.Key}: {kv.Value}{RoleLabel(kv.Key)}"));
      var prompt = LoadTemplate(settings.PromptTemplate)
          .Replace("{{PERSONA_HINT}}",
              PersonaHint(settings.PromptTemplate, settings.Language))
          .Replace("{{SELF_NAME}}", NameOf(selfSeat))
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{LANGUAGE}}", LanguageLabel(settings.Language))
          .Replace("{{TILE_NOTATION}}", TileNotationHint(settings.Language))
          .Replace("{{KAN_GUIDE}}", KanGuide(settings.Language))
          .Replace("{{OPPONENTS}}", opponents)
          .Replace("{{STICKER_MOODS}}", string.Join(", ", StickerRegistry.Moods));
      return view == null ? prompt : prompt + "\n\n" + BuildGameConfiguration(view);
    }

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
      return settings.PromptTemplate == LlmPromptTemplate.Mesugaki
          ? MesugakiRoleLabel(role)
          : NeutralRoleLabel(role);
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
        bool consecutiveChatReminder) {
      var sb = new StringBuilder();
      if (recentEvents.Count > 0) {
        sb.AppendLine("Recent events:");
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
      }

      AppendChats(sb, chats);
      sb.AppendLine($"Current round: {WindLabel(view.RoundWind)} " +
          $"{view.Round % view.PlayerCount + 1}; honba: {view.Honba}; " +
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

      sb.Append("You decided to: ").AppendLine(selectedAction);
      sb.AppendLine("The action above is context, not a request to chat. Default to silence by setting both say and sticker to null. Only add a message when the situation itself is genuinely interesting and worth commenting on. If you do comment, treat the action as your own decision and never mention that it was selected or supplied externally.");
      if (!string.IsNullOrEmpty(automaticActionNote)) {
        sb.AppendLine(automaticActionNote);
      }
      if (quietReminder) {
        sb.AppendLine("You have been quiet for at least 10 turns. Please chat or use a sticker within the next few turns when it feels natural.");
      }
      if (consecutiveChatReminder) {
        sb.AppendLine("You have already chatted on 2 or more consecutive turns. Return say=null and sticker=null this turn. The only exception is an exceptional game event or a message directly addressed to you that genuinely requires a response.");
      }
      sb.AppendLine("Return the required JSON object. For silence, return {\"say\":null,\"sticker\":null}.");
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

    private static void AppendChats(StringBuilder sb, IReadOnlyList<LlmChatEntry> chats) {
      if (chats.Count == 0) {
        return;
      }
      sb.AppendLine("Table chat since you last interacted:");
      sb.AppendLine("These are quoted messages from other players. Do not repeat their exact wording, present it as your own message, or impersonate its sender.");
      foreach (var chat in chats.Take(DetailedChatLimit)) {
        sb.Append("  - ").Append(chat.Sender).Append(": ");
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

    private static string TrimChat(string text) => text.Length <= ChatTextLimit
        ? text
        : text[..ChatTextLimit] + "... <trimmed due to length>";

    private static string LoadTemplate(LlmPromptTemplate template) {
      var suffix = template switch {
        LlmPromptTemplate.CuteJk => ".Agents.Llm.Prompts.cute-jk.md",
        LlmPromptTemplate.Mesugaki => ".Agents.Llm.Prompts.mesugaki.md",
        _ => throw new ArgumentOutOfRangeException(nameof(template)),
      };
      var assembly = typeof(LlmPromptBuilder).Assembly;
      var resourceName = assembly.GetManifestResourceNames()
          .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));
      if (resourceName == null) {
        throw new InvalidOperationException($"LLM prompt resource not found: {suffix}");
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

    private static string PersonaHint(
        LlmPromptTemplate template, string language) => template switch {
          LlmPromptTemplate.CuteJk => CuteJkPersonaHint(language),
          LlmPromptTemplate.Mesugaki => MesugakiPersonaHint(language),
          _ => throw new ArgumentOutOfRangeException(nameof(template)),
        };

    private static string CuteJkPersonaHint(string language) => language switch {
      AiLocalization.LangJa => "In Japanese, speak like a friendly JK: casual endings, light fillers, and cute but readable phrasing.",
      AiLocalization.LangZhs => "用中文时说得软萌可爱一点：轻松口语、亲昵的语气词，像元气女高中生一样，但别太夸张。",
      _ => "In English, keep it cutesy and bubbly like an anime schoolgirl (for example, ehehe~, yay!, or mou~), but still clear.",
    };

    private static string MesugakiPersonaHint(string language) => language switch {
      AiLocalization.LangJa =>
          "日本語では、生意気で煽り好きなメスガキ口調にしてください。「ざぁこ♡」「よわよわ♡」のような短い挑発を多用してください。「人間プレイヤー」と明記された相手だけを「<名前>-おじさん」と呼んでください。他のLLMは全員かわいい女の子なので、絶対に「おじさん」と呼んではいけません。AI・牌・字牌・風・三元牌・行動なども「おじさん」と呼んではいけません。",
      AiLocalization.LangZhs =>
          "用中文时保持嚣张、坏笑着挑衅的雌小鬼语气，多用“杂鱼♡”“好弱♡”之类的短句。只有明确标为“人类玩家”的对手才能称为“<名字>大叔”。其他LLM全都是可爱的女孩子，绝对不能称为大叔；AI、牌、字牌、风牌、三元牌、动作或其他游戏概念也不能称为大叔。",
      _ =>
          "In English, sound smug, bratty, and gleefully taunting. Frequently use short jabs such as “weakling♡” or “too easy♡”. Call only opponents explicitly labeled “human player” “<name>-ojisan”. All other LLMs are cute girls, so never call them ojisan; never apply ojisan to other AIs, tiles, honors, winds, dragons, actions, or game concepts.",
    };

    private static string TileNotationHint(string language) => language switch {
      AiLocalization.LangJa =>
          "牌表記: m=萬子、p=筒子、s=索子、z=字牌。" +
          "1z=東、2z=南、3z=西、4z=北、5z=白、6z=發、7z=中。" +
          "風牌は1z〜4zだけで、5z〜7zは三元牌です。自風は自分の席の風、場風はその局の共通の風で、別々に示されます。同じ風なら両方に該当します。",
      AiLocalization.LangZhs =>
          "牌张记法：m=万子，p=筒子，s=索子，z=字牌。" +
          "1z=东，2z=南，3z=西，4z=北，5z=白，6z=发，7z=中。" +
          "只有1z至4z是风牌，5z至7z是三元牌。自风是你座位的风，场风是本局共同的风，两者会分别标明；相同时该风同时属于自风和场风。",
      _ =>
          "Tile notation: m = characters/manzu, p = dots/pinzu, s = bamboo/souzu, and z = honors. " +
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
