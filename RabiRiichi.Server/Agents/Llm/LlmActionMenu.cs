using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// One legal choice represented by a stable (actionIndex, optionIndex) pair,
  /// a short machine kind, and a human description used in LLM context.
  /// </summary>
  public sealed class LlmChoice {
    /// <summary> Opaque id the LLM selects (index into the menu). </summary>
    public int Id { get; init; }
    /// <summary> Action index into the inquiry's action list. </summary>
    public int ActionIndex { get; init; }
    /// <summary>
    /// Option index within the action, or -1 for a confirm-style action.
    /// </summary>
    public int OptionIndex { get; init; }
    /// <summary> Short kind, e.g. "discard", "riichi", "pon", "ron", "skip". </summary>
    public string Kind { get; init; }
    /// <summary> Human-readable description for the prompt. </summary>
    public string Description { get; init; }

    /// <summary> Canonical serialized response payload for this choice. </summary>
    public string SerializedResponse => OptionIndex < 0
        ? "{}"
        : System.Text.Json.JsonSerializer.Serialize(OptionIndex);

    /// <summary> Builds the response for this choice. </summary>
    public InquiryResponse ToResponse(int seat) {
      return new InquiryResponse(seat, ActionIndex, SerializedResponse);
    }

    /// <summary> Whether a response selects this exact choice. </summary>
    public bool Matches(InquiryResponse response) {
      return ActionIndex == response.index && SerializedResponse == response.response;
    }
  }

  /// <summary>
  /// Flattens a <see cref="SinglePlayerInquiry"/> into compact descriptions of
  /// its legal actions. This is the only LLM-path code that understands the
  /// engine's action shapes. Choice ids are stable within one menu.
  /// </summary>
  public static class LlmActionMenu {
    /// <summary>
    /// True for reaction inquiries that could expose a chii, pon, or daiminkan
    /// opportunity. Ron is deliberately exempt so the model may react to a win.
    /// </summary>
    public static bool IsOutOfTurnCallInquiry(SinglePlayerInquiry inquiry) {
      if (inquiry.actions.Any(action => action is RonAction)) return false;
      return inquiry.actions.Any(action => action is ChiiAction or PonAction ||
          action is KanAction kan && IsDaiminkanOnly(kan));
    }

    public static string DescribeAutomaticAction(
        bool selfRiichi, IReadOnlyList<LlmChoice> menu, string selectedAction) {
      if (menu.Count != 1) return null;
      if (selfRiichi && menu[0].Kind == "discard" &&
          selectedAction.StartsWith("discard ", System.StringComparison.Ordinal)) {
        return $"You automatically discarded {selectedAction["discard ".Length..]} " +
            "after riichi because that was the only valid action.";
      }
      return "You automatically took this action because it was the only valid action: " +
          $"{selectedAction}.";
    }

    public static string DescribeSelected(SinglePlayerInquiry inquiry, InquiryResponse response) {
      var choice = Build(inquiry).FirstOrDefault(c => c.Matches(response));
      if (choice != null) return choice.Description;
      if (response.index < 0) return "pass / take no action";
      return response.index < inquiry.actions.Count
          ? inquiry.actions[response.index].GetType().Name.Replace("Action", "")
          : "take the selected legal action";
    }

    public static IReadOnlyList<LlmChoice> Build(SinglePlayerInquiry inquiry) {
      var choices = new List<LlmChoice>();
      var actions = inquiry.actions;
      for (int ai = 0; ai < actions.Count; ai++) {
        switch (actions[ai]) {
          case RiichiAction riichi:
            AddTileOptions(choices, ai, riichi, "riichi");
            break;
          case PlayTileAction play:
            AddTileOptions(choices, ai, play, "discard");
            break;
          case ChiiAction chii:
            AddTilesOptions(choices, ai, chii, "chii");
            break;
          case PonAction pon:
            AddTilesOptions(choices, ai, pon, "pon");
            break;
          case KanAction kan:
            AddKanOptions(choices, ai, kan);
            break;
          case NukiDoraAction nuki:
            AddTilesOptions(choices, ai, nuki, "nukidora");
            break;
          case RonAction:
            AddConfirm(choices, ai, "ron", "Declare RON (win on the discard)");
            break;
          case TsumoAction:
            AddConfirm(choices, ai, "tsumo", "Declare TSUMO (self-draw win)");
            break;
          case RyuukyokuAction:
            AddConfirm(choices, ai, "abortive_draw", "Declare an abortive draw");
            break;
          case NextRoundAction:
            AddConfirm(choices, ai, "next_round", "Acknowledge and continue to next round");
            break;
          case SkipAction:
            AddConfirm(choices, ai, "skip", "Do nothing / pass");
            break;
          default:
            break;
        }
      }
      return choices;
    }

    private static void AddTileOptions(
        List<LlmChoice> choices, int actionIndex, PlayTileAction action, string kind) {
      var options = action.options;
      for (int oi = 0; oi < options.Count; oi++) {
        var tile = options[oi].tile;
        var desc = $"{kind} {TileNotation.One(tile)}";
        // Attach tenpai/value info when discarding this tile leaves tenpai.
        var cand = action.candidates?.Find(c => c.tile.tile.IsSame(tile.tile));
        if (cand != null && cand.tenpaiInfos.Count > 0) {
          var best = cand.tenpaiInfos.OrderByDescending(t => t.han * 100 + t.points).First();
          var waits = string.Join("", cand.tenpaiInfos
              .Select(t => t.winningTile).Distinct().Select(TileNotation.One));
          desc += $" -> TENPAI (wait {waits}, up to {best.han} han)";
        }
        choices.Add(new LlmChoice {
          Id = choices.Count,
          ActionIndex = actionIndex,
          OptionIndex = oi,
          Kind = kind,
          Description = desc,
        });
      }
    }

    private static void AddTilesOptions(
        List<LlmChoice> choices, int actionIndex, ChooseTilesAction action, string kind) {
      var options = action.options;
      for (int oi = 0; oi < options.Count; oi++) {
        var tiles = options[oi].tiles;
        choices.Add(new LlmChoice {
          Id = choices.Count,
          ActionIndex = actionIndex,
          OptionIndex = oi,
          Kind = kind,
          Description = $"{kind} using {TileNotation.Group(tiles)}",
        });
      }
    }

    private static void AddKanOptions(
        List<LlmChoice> choices, int actionIndex, KanAction action) {
      var options = action.options;
      for (var optionIndex = 0; optionIndex < options.Count; optionIndex++) {
        var tiles = options[optionIndex].tiles;
        var (kind, explanation) = KanDescription(new Kan(tiles).KanSource);
        choices.Add(new LlmChoice {
          Id = choices.Count,
          ActionIndex = actionIndex,
          OptionIndex = optionIndex,
          Kind = kind,
          Description = $"{kind} ({explanation}) using {TileNotation.Group(tiles)}",
        });
      }
    }

    private static bool IsDaiminkanOnly(KanAction action) =>
        action.options.Count > 0 &&
        action.options.All(option => new Kan(option.tiles).KanSource == TileSource.Daiminkan);

    private static (string Kind, string Explanation) KanDescription(TileSource source) => source switch {
      TileSource.Ankan => ("ankan", "closed kan from your own hand"),
      TileSource.Kakan => ("kakan", "added kan upgrading an existing pon"),
      TileSource.Daiminkan => ("daiminkan", "open kan on another player's discard"),
      _ => ("kan", "four-of-a-kind call"),
    };

    private static void AddConfirm(
        List<LlmChoice> choices, int actionIndex, string kind, string desc) {
      choices.Add(new LlmChoice {
        Id = choices.Count,
        ActionIndex = actionIndex,
        OptionIndex = -1,
        Kind = kind,
        Description = desc,
      });
    }
  }
}
