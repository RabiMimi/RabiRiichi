using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Actions;
using RabiRiichi.Core;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// One selectable choice presented to the LLM: a stable (actionIndex,
  /// optionIndex) pair, a short machine kind, and a human description. The LLM
  /// replies with the <see cref="Id"/>; we map it straight back to an
  /// <see cref="InquiryResponse"/>.
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

    /// <summary> Builds the response for this choice. </summary>
    public InquiryResponse ToResponse(int seat) {
      return OptionIndex < 0
          ? new InquiryResponse(seat, ActionIndex, "{}")
          : new InquiryResponse(seat, ActionIndex,
              System.Text.Json.JsonSerializer.Serialize(OptionIndex));
    }
  }

  /// <summary>
  /// Flattens a <see cref="SinglePlayerInquiry"/> into a compact, numbered menu
  /// of <see cref="LlmChoice"/>s. This is the ONLY place that understands the
  /// engine's action shapes for the LLM path, keeping the agent decoupled from
  /// action internals. Choice ids are stable within one menu.
  /// </summary>
  public static class LlmActionMenu {
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
            AddTilesOptions(choices, ai, kan, "kan");
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
