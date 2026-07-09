using RabiRiichi.Actions;
using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// The pure decision logic for <see cref="RuleBasedAI"/>, separated so it can
  /// be unit tested without the server/room plumbing.
  ///
  /// Every method takes a <see cref="PublicGameView"/> (fair information only)
  /// and the per-seat <see cref="SinglePlayerInquiry"/>, and returns an
  /// <see cref="InquiryResponse"/> the engine understands.
  /// </summary>
  public static class RuleBasedStrategy {
    /// <summary>
    /// Chooses a response for the given inquiry. Actions are considered in
    /// descending value: winning &gt; riichi &gt; discard &gt; call &gt; skip.
    /// </summary>
    public static InquiryResponse Decide(PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;

      // 1. Always take a valid win.
      int agariIdx = actions.FindIndex(a => a is AgariAction);
      if (agariIdx >= 0) {
        return Confirm(view.Seat, agariIdx);
      }

      // 2. Abortive draw (e.g. kyuushu kyuuhai) - accept when offered, it avoids
      //    a likely losing hand.
      int ryuukyokuIdx = actions.FindIndex(a => a is RyuukyokuAction);
      if (ryuukyokuIdx >= 0) {
        return Confirm(view.Seat, ryuukyokuIdx);
      }

      // 3. Riichi: prefer declaring when it is worthwhile.
      int riichiIdx = actions.FindIndex(a => a is RiichiAction);
      if (riichiIdx >= 0 && actions[riichiIdx] is RiichiAction riichi
          && ShouldRiichi(view, riichi)) {
        int tileIdx = ChooseDiscardIndex(view, riichi);
        return Choose(view.Seat, riichiIdx, tileIdx);
      }

      // 4. Plain discard (our own turn).
      int discardIdx = actions.FindIndex(a => a is PlayTileAction and not RiichiAction);
      if (discardIdx >= 0 && actions[discardIdx] is PlayTileAction play) {
        int tileIdx = ChooseDiscardIndex(view, play);
        return Choose(view.Seat, discardIdx, tileIdx);
      }

      // 5. Calls (chii/pon/kan) - only when clearly beneficial.
      int callIdx = ChooseCall(view, actions);
      if (callIdx >= 0 && actions[callIdx] is IChoiceAction) {
        // Take the first (best) offered grouping for the chosen call.
        return Choose(view.Seat, callIdx, 0);
      }

      // 6. Confirm-style actions we didn't special-case (e.g. next round).
      int nextRoundIdx = actions.FindIndex(a => a is NextRoundAction);
      if (nextRoundIdx >= 0) {
        return Confirm(view.Seat, nextRoundIdx);
      }

      // 7. Otherwise decline / take the safe default.
      return InquiryResponse.Default(view.Seat);
    }

    #region Discard selection

    /// <summary>
    /// Chooses which option index of a discard action to play. The primary
    /// criterion is hand advancement - minimize shanten, then maximize ukeire
    /// (live acceptance) - so the AI actively pushes toward a win. Ties are broken
    /// by tile value and, when an opponent has declared riichi, by safety.
    /// </summary>
    public static int ChooseDiscardIndex(PublicGameView view, PlayTileAction action) {
      var options = action.options;
      if (options.Count == 0) {
        return 0;
      }

      bool underThreat = view.OpponentSeats.Any(view.IsRiichi);

      // The 14 tiles we are choosing a discard from (own concealed tiles + draw).
      var hand14 = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        hand14.Add(view.SelfHand.pendingTile);
      }

      int bestIdx = 0;
      double bestScore = double.NegativeInfinity;
      for (int i = 0; i < options.Count; i++) {
        var tile = options[i].tile;
        double score = ScoreDiscard(view, action, tile, hand14, underThreat);
        if (score > bestScore) {
          bestScore = score;
          bestIdx = i;
        }
      }
      return bestIdx;
    }

    // Weights: shanten dominates everything (a lower shanten hand is always
    // preferred), ukeire is the next tie-breaker, then value/safety fine-tune.
    private const double ShantenWeight = 100000;
    private const double UkeireWeight = 100;

    /// <summary>
    /// Higher is a better tile to DISCARD. Rewards the discard that leaves the
    /// hand closest to a win (lowest shanten, widest live acceptance), then
    /// prefers letting go of low-value tiles, and biases toward safe tiles when
    /// under threat.
    /// </summary>
    private static double ScoreDiscard(
        PublicGameView view, PlayTileAction action, GameTile tile,
        IReadOnlyList<GameTile> hand14, bool underThreat) {
      double score = 0;

      // Primary: advance the hand. Fewer shanten is strictly better; among equal
      // shanten, more ukeire (live tiles that improve the hand) is better.
      var eval = view.EvaluateDiscard(tile, hand14);
      score -= eval.Shanten * ShantenWeight; // lower shanten -> higher score
      score += eval.Ukeire * UkeireWeight;

      // At tenpai, prefer the wait that scores the most (server-computed value).
      if (eval.Shanten == 0) {
        var candidate = action.candidates?.Find(c => c.tile.tile.IsSame(tile.tile));
        if (candidate != null && candidate.tenpaiInfos.Count > 0) {
          int bestValue = candidate.tenpaiInfos.Max(t => t.han * 100 + (int)t.points);
          score += bestValue * 0.5;
        }
      }

      // Tile value: discarding a valuable tile is costly, so lower its discard
      // score. Isolated terminals/honors are the cheapest to let go.
      score -= TileKeepValue(view, tile.tile) * 5;

      // Safety: when threatened, prefer tiles unlikely to deal in. Kept small
      // relative to shanten so defense refines, but does not override, pushing a
      // fast hand; a dedicated fold heuristic could be layered on later.
      if (underThreat) {
        score += SafetyScore(view, tile.tile) * 50;
      }

      return score;
    }

    /// <summary>
    /// How much we want to KEEP a tile (higher = more valuable to hold, so worse
    /// to discard). Rewards dora, yakuhai, and central tiles for their shape
    /// flexibility.
    /// </summary>
    private static int TileKeepValue(PublicGameView view, Tile tile) {
      int value = 0;
      value += view.CountDora(tile); // aka/dora carriers are worth keeping
      if (tile.Akadora) {
        value += 2;
      }
      if (IsValueHonor(view, tile)) {
        value += 2;
      }
      if (tile.IsMPS) {
        // Central tiles (4-6) make the most shapes; terminals the fewest.
        int n = tile.Num;
        value += n is >= 4 and <= 6 ? 2 : (n is 1 or 9 ? 0 : 1);
      }
      return value;
    }

    /// <summary> Yakuhai for this player: dragons, round wind, seat wind. </summary>
    private static bool IsValueHonor(PublicGameView view, Tile tile) {
      if (!tile.IsZ) {
        return false;
      }
      if (tile.IsSangen) {
        return true;
      }
      return tile.IsSame(Tile.From(view.RoundWind)) || tile.IsSame(Tile.From(view.SelfWind));
    }

    #endregion

    #region Safety (defense against riichi)

    /// <summary>
    /// A rough danger estimate: higher = safer to discard. Uses only public
    /// discards. Genbutsu (already discarded by every riichi opponent) is fully
    /// safe; suji and low-count tiles are somewhat safer; live central tiles are
    /// the most dangerous.
    /// </summary>
    public static double SafetyScore(PublicGameView view, Tile tile) {
      var riichiSeats = view.OpponentSeats.Where(view.IsRiichi).ToList();
      if (riichiSeats.Count == 0) {
        return 0;
      }

      double minSafety = double.PositiveInfinity;
      foreach (var seat in riichiSeats) {
        double safety = SafetyAgainst(view, seat, tile);
        if (safety < minSafety) {
          minSafety = safety;
        }
      }
      return minSafety;
    }

    private static double SafetyAgainst(PublicGameView view, int seat, Tile tile) {
      // Genbutsu: this exact tile sits in the opponent's discard river -> 100% safe.
      if (view.DiscardsOf(seat).Any(t => t.tile.IsSame(tile))) {
        return 10;
      }
      // Also safe if it is in ANY player's discards after this opponent declared
      // riichi is stronger, but as a simple non-cheating heuristic we treat the
      // opponent's own river (above) as the reliable genbutsu source.

      // Honors: safer the fewer are live (three seen -> the 4th is safe-ish).
      if (tile.IsZ) {
        int seen = view.VisibleCount(tile);
        return seen >= 3 ? 8 : (seen == 2 ? 5 : 2);
      }

      // Suji: if the opponent discarded the tile 3 away on the same side, a
      // ryanmen wait on this tile is impossible (classic suji defense).
      if (HasSuji(view, seat, tile)) {
        return 6;
      }

      // Terminals are safer than central tiles (fewer ryanmen shapes reach them).
      int n = tile.Num;
      if (n is 1 or 9) {
        return 4;
      }
      if (n is 2 or 8) {
        return 3;
      }
      // Central 3-7: most dangerous.
      return 1;
    }

    /// <summary> Whether <paramref name="tile"/> is suji off the seat's discards. </summary>
    private static bool HasSuji(PublicGameView view, int seat, Tile tile) {
      if (!tile.IsMPS) {
        return false;
      }
      int n = tile.Num;
      var suit = tile.Suit;
      bool DiscardedNum(int num) =>
          num is >= 1 and <= 9
          && view.DiscardsOf(seat).Any(t => {
            var dt = t.tile;
            return dt.Suit == suit && dt.Num == num;
          });

      // 4-6 need both suji halves; 1-3 and 7-9 need the single 3-away tile.
      return n switch {
        1 or 2 or 3 => DiscardedNum(n + 3),
        7 or 8 or 9 => DiscardedNum(n - 3),
        _ => DiscardedNum(n - 3) && DiscardedNum(n + 3),
      };
    }

    #endregion

    #region Riichi decision

    /// <summary>
    /// Declare riichi when concealed, with enough tiles left to draw, and the
    /// resulting wait is not trivially dead. A dealer or a decent wait riichis
    /// readily; with almost no tiles left, prefer damaten/keep flexible.
    /// </summary>
    public static bool ShouldRiichi(PublicGameView view, RiichiAction action) {
      // Not worth declaring if the wall is nearly exhausted (can't draw/deal-in
      // risk without upside).
      if (view.WallRemaining < 4) {
        return false;
      }

      // Pick the discard we would riichi on and check its wait breadth.
      int idx = ChooseDiscardIndex(view, action);
      var candidate = action.candidates?.Find(
          c => idx < action.options.Count && c.tile.tile.IsSame(action.options[idx].tile.tile));
      if (candidate == null || candidate.tenpaiInfos.Count == 0) {
        // No tenpai info: fall back to declaring (the action is only offered when
        // tenpai), riichi still adds a han and pressure.
        return true;
      }

      int liveWaits = candidate.tenpaiInfos.Sum(t => view.UnseenCount(t.winningTile));
      // Avoid riichi on a fully dead wait; otherwise declare.
      return liveWaits > 0;
    }

    #endregion

    #region Call decision

    /// <summary>
    /// Decide whether to call a meld. Conservative: only call to secure a
    /// yakuhai triplet (a clear, fast yaku). Otherwise stay concealed to preserve
    /// menzen value and riichi potential.
    /// Returns the action index to take, or -1 to skip.
    /// </summary>
    public static int ChooseCall(PublicGameView view, IReadOnlyList<IPlayerAction> actions) {
      // Prefer pon of a value honor (fast, guaranteed yaku).
      for (int i = 0; i < actions.Count; i++) {
        if (actions[i] is PonAction pon && pon.options.Count > 0) {
          var tiles = pon.options[0].tiles;
          if (tiles.Count > 0 && IsValueHonor(view, tiles[0].tile)) {
            return i;
          }
        }
      }
      // Everything else (chii, non-yakuhai pon, most kan): skip to stay concealed.
      return -1;
    }

    #endregion

    #region Response encoding

    /// <summary> A single-choice response selecting option <paramref name="optionIndex"/>. </summary>
    private static InquiryResponse Choose(int seat, int actionIndex, int optionIndex) {
      return new InquiryResponse(seat, actionIndex, JsonSerializer.Serialize(optionIndex));
    }

    /// <summary> A confirm response (Empty payload) for the given action. </summary>
    private static InquiryResponse Confirm(int seat, int actionIndex) {
      return new InquiryResponse(seat, actionIndex, "{}");
    }

    #endregion
  }
}
