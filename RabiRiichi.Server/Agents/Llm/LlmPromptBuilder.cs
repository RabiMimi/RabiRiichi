using RabiRiichi.Core;
using RabiRiichi.Server.Generated.Rpc;
using System.Text;

namespace RabiRiichi.Server.Agents.Llm {
  public sealed record LlmChatEntry(string Sender, string Text, string Sticker);

  /// <summary>Builds prompts from an embedded markdown persona template.</summary>
  public sealed class LlmPromptBuilder(
      LlmSettings settings,
      IReadOnlyDictionary<int, string> seatNames) {
    private const int DetailedChatLimit = 10;
    private const int ChatTextLimit = 256;
    private readonly LlmSettings settings = settings;
    private readonly IReadOnlyDictionary<int, string> seatNames = seatNames;

    private string NameOf(int seat) =>
        seatNames.TryGetValue(seat, out var n) ? n : $"P{seat}";

    public static bool ShouldSendConsecutiveChatReminder(int consecutiveChatTurns) =>
        consecutiveChatTurns >= 2;

    public string BuildSystemPrompt(int selfSeat) {
      var opponents = string.Join("\n", seatNames
          .Where(kv => kv.Key != selfSeat)
          .OrderBy(kv => kv.Key)
          .Select(kv => $"- seat {kv.Key}: {kv.Value}"));
      return LoadTemplate(settings.PromptTemplate)
          .Replace("{{PERSONA_HINT}}", PersonaHint(settings.Language))
          .Replace("{{SELF_NAME}}", NameOf(selfSeat))
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{LANGUAGE}}", LanguageLabel(settings.Language))
          .Replace("{{OPPONENTS}}", opponents)
          .Replace("{{STICKER_MOODS}}", string.Join(", ", StickerRegistry.Moods));
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
            : TileNotation.Group(doraIndicators));
      sb.Append("Indicated dora tile(s): ")
        .AppendLine(doraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(doraIndicators.Select(tile => tile.NextDora)));
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
          sb.Append($"  {NameOf(s)} discards: ").AppendLine(TileNotation.Group(discards));
        }
        var called = view.CalledOf(s);
        if (called.Count > 0) {
          sb.Append($"  {NameOf(s)} melds: ")
            .AppendLine(string.Join(" ", called.Select(TileNotation.Meld)));
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

    private static string DescribeSelfHand(PublicGameView view) {
      var hand = view.SelfHand;
      var sb = new StringBuilder(TileNotation.Group(hand.freeTiles));
      if (hand.pendingTile != null)
        sb.Append(" + drew ").Append(TileNotation.One(hand.pendingTile));
      if (hand.called.Count > 0) {
        sb.Append(" | melds: ").Append(string.Join(" ", hand.called.Select(TileNotation.Meld)));
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

    private static string PersonaHint(string language) => language switch {
      AiLocalization.LangJa => "In Japanese, speak like a friendly JK: casual endings, light fillers, and cute but readable phrasing.",
      AiLocalization.LangZhs => "用中文时说得软萌可爱一点：轻松口语、亲昵的语气词，像元气女高中生一样，但别太夸张。",
      _ => "In English, keep it cutesy and bubbly like an anime schoolgirl (for example, ehehe~, yay!, or mou~), but still clear.",
    };
  }
}
