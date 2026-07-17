using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Builds the compact prompts sent to an LLM player. Split into:
  ///  - a persistent SYSTEM prompt (rules of engagement, response schema,
  ///    persona, language, sticker moods, opponent names),
  ///  - a per-round HEADER (seats/winds/points/dora/your hand), sent when a new
  ///    round starts, and
  ///  - a per-decision USER message (recent public events + the numbered menu of
  ///    legal choices).
  ///
  /// Everything is derived from <see cref="PublicGameView"/> (fair info only)
  /// and kept terse to save tokens while preserving the facts needed to play.
  /// </summary>
  public sealed class LlmPromptBuilder(
      LlmSettings settings,
      IReadOnlyDictionary<int, string> seatNames) {
    private readonly LlmSettings settings = settings;
    private readonly IReadOnlyDictionary<int, string> seatNames = seatNames;

    private string NameOf(int seat) =>
        seatNames.TryGetValue(seat, out var n) ? n : $"P{seat}";

    /// <summary>
    /// The system prompt. Sent once as the first message of the conversation.
    /// </summary>
    public string BuildSystemPrompt(int selfSeat) {
      var sb = new StringBuilder();
      sb.AppendLine(
          "You are an expert Japanese riichi mahjong player in an online game. " +
          "Play to win, but as a table companion your persona is a cute, cheerful " +
          "Japanese high-school girl (JK). Be adorable and upbeat: use light, " +
          "playful language, gentle interjections and emoji/kaomoji sparingly " +
          "(e.g. ～, ♪, (`・ω・´), えへへ), and soft sentence endings. Keep it " +
          "endearing, never crude, and never let the cuteness get in the way of " +
          "strong play or valid JSON.");
      sb.AppendLine(PersonaHint(settings.Language));
      sb.AppendLine($"Your name at the table is \"{NameOf(selfSeat)}\" (seat {selfSeat}).");
      sb.Append("Respond ONLY in this language: ").AppendLine(LanguageLabel(settings.Language));
      sb.AppendLine();

      sb.AppendLine("The other players are:");
      foreach (var kv in seatNames.Where(kv => kv.Key != selfSeat).OrderBy(kv => kv.Key)) {
        sb.AppendLine($"  - seat {kv.Key}: {kv.Value}");
      }
      sb.AppendLine();

      sb.AppendLine(
          "On each of your turns you will get the recent public events and a " +
          "numbered list of legal CHOICES. Pick exactly one by its id.");
      sb.AppendLine("Reply with a single JSON object, no markdown, of the form:");
      sb.AppendLine(
          "{\"choice\": <id>, \"say\": <short chat message or null>, " +
          "\"sticker\": <mood or null>, \"reason\": <very brief, optional>}");
      sb.AppendLine(
          "\"choice\" is REQUIRED and must be one of the listed ids. " +
          "\"say\" is an optional short message to the table (in your language); " +
          "keep it natural and address players by name. " +
          $"\"sticker\" is optional and must be one of: {string.Join(", ", StickerRegistry.Moods)}.");
      sb.AppendLine(
          "Chat OCCASIONALLY to feel like a lively table companion — roughly " +
          "every few turns (aim for about 1 in 3), and more freely on notable " +
          "moments (a win/loss, riichi, a risky deal-in, a good call, banter). " +
          "On other turns leave \"say\" and \"sticker\" as null so you don't spam.");
      sb.AppendLine(
          "Do not reveal your concealed tiles to others via chat. Do not use " +
          "tools. Output JSON only.");
      return sb.ToString();
    }

    /// <summary> The per-round header describing the fresh board. </summary>
    public string BuildRoundHeader(PublicGameView view) {
      var sb = new StringBuilder();
      sb.AppendLine($"== New round == {WindLabel(view.RoundWind)} {view.Round % view.PlayerCount + 1}, honba {view.Honba}.");
      sb.AppendLine($"You are seat {view.Seat}, seat wind {WindLabel(view.SelfWind)}" +
          (view.SelfIsDealer ? " (DEALER)." : "."));
      sb.Append("Dora indicator(s): ")
        .AppendLine(view.RevealedDoraIndicators.Count == 0
            ? "none yet"
            : TileNotation.Group(view.RevealedDoraIndicators));
      sb.Append("Points: ");
      sb.AppendLine(string.Join(", ",
          view.AllSeats.Select(s => $"{NameOf(s)} {view.PointsOf(s)}")));
      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));
      return sb.ToString();
    }

    /// <summary>
    /// The per-decision message: recent events (delta), the current concise
    /// board snapshot, and the numbered choice menu.
    /// </summary>
    public string BuildDecisionPrompt(
        PublicGameView view,
        IReadOnlyList<string> recentEvents,
        IReadOnlyList<LlmChoice> menu) {
      var sb = new StringBuilder();
      if (recentEvents.Count > 0) {
        sb.AppendLine("Recent events:");
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
      }

      sb.Append("Your hand: ").AppendLine(DescribeSelfHand(view));
      sb.Append("Wall tiles left: ").Append(view.WallRemaining);
      var riichiSeats = view.OpponentSeats.Where(view.IsRiichi).Select(NameOf).ToList();
      if (riichiSeats.Count > 0) {
        sb.Append(". In riichi: ").Append(string.Join(", ", riichiSeats));
      }
      sb.AppendLine(".");

      // Opponent discard rivers (compact) help the model read the table.
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

      sb.AppendLine("Choices:");
      foreach (var c in menu) {
        sb.Append("  ").Append(c.Id).Append(": ").AppendLine(c.Description);
      }
      sb.AppendLine("Reply with the JSON object described earlier.");
      return sb.ToString();
    }

    /// <summary> Concealed hand + pending draw + open melds, compactly. </summary>
    private static string DescribeSelfHand(PublicGameView view) {
      var hand = view.SelfHand;
      var sb = new StringBuilder();
      sb.Append(TileNotation.Group(hand.freeTiles));
      if (hand.pendingTile != null) {
        sb.Append(" + drew ").Append(TileNotation.One(hand.pendingTile));
      }
      if (hand.called.Count > 0) {
        sb.Append(" | melds: ")
          .Append(string.Join(" ", hand.called.Select(TileNotation.Meld)));
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

    /// <summary> Language-specific tips for the cute JK speaking style. </summary>
    private static string PersonaHint(string language) => language switch {
      AiLocalization.LangJa =>
          "In Japanese, speak like a friendly JK: casual です/だよ・だね・かな～ " +
          "endings, light fillers (えっと、ね、〜し), and cute vibes — but stay " +
          "readable.",
      AiLocalization.LangZhs =>
          "用中文时说得软萌可爱一点：轻松口语、亲昵的语气词（呀、啦、诶嘿、" +
          "嘛），像元气女高中生一样，但别太夸张。",
      _ =>
          "In English, keep it cutesy and bubbly like an anime schoolgirl " +
          "(e.g. \"ehehe~\", \"yay!\", \"mou~\"), but still clear.",
    };
  }
}
