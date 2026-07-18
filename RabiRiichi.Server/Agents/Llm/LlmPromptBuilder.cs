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

    public static bool ShouldSendConsecutiveChatReminder(int consecutiveChatTurns) =>
        consecutiveChatTurns >= 2;

    public string BuildSystemPrompt(int selfSeat) {
      var opponents = string.Join("\n", seatNames
          .Where(kv => kv.Key != selfSeat)
          .OrderBy(kv => kv.Key)
          .Select(kv => $"- seat {kv.Key}: {kv.Value}{RoleLabel(kv.Key)}"));
      return LoadTemplate(settings.PromptTemplate)
          .Replace("{{PERSONA_HINT}}",
              PersonaHint(settings.PromptTemplate, settings.Language))
          .Replace("{{SELF_NAME}}", NameOf(selfSeat))
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{LANGUAGE}}", LanguageLabel(settings.Language))
          .Replace("{{TILE_NOTATION}}", TileNotationHint(settings.Language))
          .Replace("{{KAN_GUIDE}}", KanGuide(settings.Language))
          .Replace("{{OPPONENTS}}", opponents)
          .Replace("{{STICKER_MOODS}}", string.Join(", ", StickerRegistry.Moods));
    }

    private string RoleLabel(int seat) {
      if (seatRoles == null || !seatRoles.TryGetValue(seat, out var role)) return "";
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
      sb.AppendLine($"Round wind (prevailing wind): {WindLabel(view.RoundWind)}. " +
          $"Your seat wind: {WindLabel(view.SelfWind)}" +
          (view.SelfIsDealer ? " (you are dealer)." : "."));
      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));
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
      sb.AppendLine("Speak as though this was entirely your own choice. Do not comment merely because an action is shown; only comment on the situation or your reasoning when it is genuinely interesting.");
      if (!string.IsNullOrEmpty(automaticActionNote)) {
        sb.AppendLine(automaticActionNote);
      }
      if (quietReminder) {
        sb.AppendLine("You have been quiet for at least 10 turns. Please chat or use a sticker within the next few turns when it feels natural.");
      }
      if (consecutiveChatReminder) {
        sb.AppendLine("You have chatted on 2 or more consecutive turns. Do not chat or use a sticker this turn unless the situation is exceptional or someone directly addressed you.");
      }
      sb.AppendLine("Reply with the JSON object described earlier.");
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
