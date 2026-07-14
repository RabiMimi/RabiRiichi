using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
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

      // 4. Self-turn set-asides that compete with the discard: kan (ankan/kakan)
      //    and nukidora. These are only worth taking when they clearly help, so
      //    they are evaluated BEFORE the plain discard - otherwise the AI would
      //    always fall through to discarding and never kan/nuki on its own turn.
      var selfCall = ChooseSelfTurnCall(view, actions);
      if (selfCall != null) {
        return selfCall;
      }

      // 5. Plain discard (our own turn).
      int discardIdx = actions.FindIndex(a => a is PlayTileAction and not RiichiAction);
      if (discardIdx >= 0 && actions[discardIdx] is PlayTileAction play) {
        int tileIdx = ChooseDiscardIndex(view, play);
        return Choose(view.Seat, discardIdx, tileIdx);
      }

      // 6. Reactive calls on another player's discard (chii/pon/daiminkan) - only
      //    when they clearly advance a yaku-bearing hand.
      var reactiveCall = ChooseReactiveCall(view, actions);
      if (reactiveCall != null) {
        return reactiveCall;
      }

      // 7. Confirm-style actions we didn't special-case (e.g. next round).
      int nextRoundIdx = actions.FindIndex(a => a is NextRoundAction);
      if (nextRoundIdx >= 0) {
        return Confirm(view.Seat, nextRoundIdx);
      }

      // 8. Otherwise decline / take the safe default.
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

    #region Call decision - self turn (kan / nukidora)

    /// <summary>
    /// Classifies a <see cref="KanAction"/> option by matching it against the
    /// viewer's public state: an option whose 3 base tiles already form a called
    /// pon is a kakan; an option containing a tile taken from a discard is a
    /// daiminkan; otherwise it is a concealed ankan.
    /// </summary>
    private enum KanKind { Ankan, Kakan, Daiminkan }

    private static KanKind ClassifyKan(PublicGameView view, IReadOnlyList<GameTile> option) {
      // Kakan: three of the four tiles already sit in an existing called pon.
      if (option.Count > 0
          && view.SelfCalled.Any(m => m is Kou && m.IsSame(MenLikeOf(option[0])))) {
        return KanKind.Kakan;
      }
      // Daiminkan: one of the tiles came from an opponent's discard (not tsumo).
      if (option.Any(t => !t.IsTsumo)) {
        return KanKind.Daiminkan;
      }
      return KanKind.Ankan;
    }

    // A throwaway single-tile Kou-key wrapper so we can reuse MenLike.IsSame for
    // "same kind" comparison against a called pon.
    private static Kou MenLikeOf(GameTile tile) =>
        new(new[] { tile, tile, tile });

    /// <summary> First index matching the predicate, or -1 (IReadOnlyList has no FindIndex). </summary>
    private static int IndexOfAction(
        IReadOnlyList<IPlayerAction> actions, System.Func<IPlayerAction, bool> pred) {
      for (int i = 0; i < actions.Count; i++) {
        if (pred(actions[i])) {
          return i;
        }
      }
      return -1;
    }

    /// <summary>
    /// Considers self-turn set-asides (ankan, kakan, nukidora) that compete with
    /// the discard. Returns a response if one is worth taking now, else null so
    /// the caller proceeds to the normal discard. A strong player kans/nukis
    /// aggressively for the extra dora and rinshan draw, but never while folding
    /// or when it would damage the hand.
    /// </summary>
    private static InquiryResponse ChooseSelfTurnCall(
        PublicGameView view, IReadOnlyList<IPlayerAction> actions) {
      // Nukidora (3-player North): always beneficial - a free dora that does not
      // break concealment. Take it whenever offered.
      int nukiIdx = IndexOfAction(actions, a => a is NukiDoraAction);
      if (nukiIdx >= 0 && actions[nukiIdx] is NukiDoraAction nuki && nuki.options.Count > 0) {
        return Choose(view.Seat, nukiIdx, 0);
      }

      int kanIdx = IndexOfAction(actions, a => a is KanAction);
      if (kanIdx < 0 || actions[kanIdx] is not KanAction kan || kan.options.Count == 0) {
        return null;
      }

      int bestOption = ChooseSelfKanOption(view, kan);
      return bestOption >= 0 ? Choose(view.Seat, kanIdx, bestOption) : null;
    }

    /// <summary>
    /// Picks which kan option to declare on our own turn, or -1 to decline.
    /// Evaluates every offered ankan/kakan and keeps the most valuable safe one.
    /// </summary>
    private static int ChooseSelfKanOption(PublicGameView view, KanAction kan) {
      // Under a fold (an opponent is in riichi and we are not close to winning),
      // avoid kan: it reveals a fresh dora that can only help the aggressor and
      // gives up a safe discard.
      bool folding = view.OpponentSeats.Any(view.IsRiichi)
          && !view.SelfRiichi
          && SelfBestShanten(view) > 0;

      int bestIdx = -1;
      double bestScore = 0;
      for (int i = 0; i < kan.options.Count; i++) {
        var option = kan.options[i].tiles;
        if (option.Count == 0) {
          continue;
        }
        var kind = ClassifyKan(view, option);
        double score = ScoreSelfKan(view, option, kind, folding);
        if (score > bestScore) {
          bestScore = score;
          bestIdx = i;
        }
      }
      return bestIdx;
    }

    /// <summary>
    /// Value of declaring a given ankan/kakan (higher = better; 0 or below means
    /// decline). We reward the guaranteed extra dora indicator and rinshan draw,
    /// but require the kan not to hurt the hand: an ankan must not raise our
    /// shanten, and we do not kan while folding.
    /// </summary>
    private static double ScoreSelfKan(
        PublicGameView view, IReadOnlyList<GameTile> option, KanKind kind, bool folding) {
      if (folding) {
        return 0;
      }
      // The wall must be able to supply a replacement tile; if not, the engine
      // would not have offered it, but guard anyway.
      if (view.WallRemaining <= 0) {
        return 0;
      }

      double dora = option.Sum(t => view.CountDora(t.tile) + (t.tile.Akadora ? 1 : 0));
      double baseValue = 5 + dora * 3; // rinshan chance + guaranteed new dora reveal

      if (kind == KanKind.Kakan) {
        // Kakan almost always gains value (upgrades an existing pon, new dora,
        // rinshan). The only real risk is being robbed (chankan); we still take
        // it because we are pushing, but slightly prefer it when tenpai.
        if (SelfBestShanten(view) == 0) {
          baseValue += 3;
        }
        return baseValue;
      }

      // Ankan: only if it does not worsen our shanten. Compare the hand's shanten
      // keeping the four tiles concealed vs. setting them aside as a kan.
      int before = SelfBestShanten(view);
      int after = ShantenIfAnkan(view, option);
      if (after > before) {
        return 0; // the kan breaks our shape - decline
      }
      // A neutral or improving ankan is good; reward keeping/advancing tenpai.
      if (after == 0) {
        baseValue += 2;
      }
      return baseValue;
    }

    /// <summary> Shanten of our current concealed hand (free + pending draw). </summary>
    private static int SelfBestShanten(PublicGameView view) {
      var hand = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        hand.Add(view.SelfHand.pendingTile);
      }
      return view.ShantenOf(hand);
    }

    /// <summary>
    /// Shanten after setting aside the four ankan tiles: they leave the free
    /// tiles and become a concealed meld, so the remaining hand must still be a
    /// legal waiting shape.
    /// </summary>
    private static int ShantenIfAnkan(PublicGameView view, IReadOnlyList<GameTile> option) {
      var hand = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        hand.Add(view.SelfHand.pendingTile);
      }
      foreach (var t in option) {
        hand.RemoveAll(h => ReferenceEquals(h, t));
      }
      var called = new List<MenLike>(view.SelfCalled) { new Kan(option, TileSource.Ankan) };
      return view.ShantenOf(hand, called);
    }

    #endregion

    #region Call decision - reactive (chii / pon / daiminkan)

    /// <summary>
    /// Considers calling on another player's discard (chii, pon, or daiminkan).
    /// Opening the hand sacrifices menzen (and riichi), so a strong player only
    /// calls when it advances a hand that will still have a yaku and is worth
    /// opening for. Returns a response, or null to stay concealed.
    /// </summary>
    private static InquiryResponse ChooseReactiveCall(
        PublicGameView view, IReadOnlyList<IPlayerAction> actions) {
      // Never open the hand once we are in riichi or already tenpai concealed with
      // a live wait - the concealed hand is worth more.
      if (view.SelfRiichi) {
        return null;
      }

      int bestActionIdx = -1;
      int bestOptionIdx = 0;
      double bestScore = 0;
      for (int i = 0; i < actions.Count; i++) {
        if (actions[i] is not IChoiceAction) {
          continue;
        }
        switch (actions[i]) {
          case PonAction pon:
            EvaluateMeldCall(view, pon.options, isChii: false,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
          case ChiiAction chii:
            EvaluateMeldCall(view, chii.options, isChii: true,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
          case KanAction daiminkan:
            EvaluateDaiminkan(view, daiminkan.options,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
        }
      }

      return bestActionIdx >= 0
          ? Choose(view.Seat, bestActionIdx, bestOptionIdx)
          : null;
    }

    /// <summary>
    /// Scores every grouping of a pon/chii and keeps the best one across all call
    /// actions. A call is only accepted when it strictly advances the hand
    /// (lowers shanten) AND the resulting open hand can still hold a yaku.
    /// </summary>
    private static void EvaluateMeldCall(
        PublicGameView view, IReadOnlyList<ChooseTilesActionOption> options, bool isChii,
        ref double bestScore, ref int bestActionIdx, ref int bestOptionIdx, int actionIndex) {
      for (int j = 0; j < options.Count; j++) {
        var tiles = options[j].tiles;
        if (tiles.Count == 0) {
          continue;
        }
        double score = ScoreOpenCall(view, tiles, isChii);
        if (score > bestScore) {
          bestScore = score;
          bestActionIdx = actionIndex;
          bestOptionIdx = j;
        }
      }
    }

    /// <summary>
    /// Value of opening with a pon/chii using <paramref name="calledTiles"/>
    /// (the meld's tiles, including the claimed discard). Returns 0 to decline.
    /// </summary>
    private static double ScoreOpenCall(
        PublicGameView view, IReadOnlyList<GameTile> calledTiles, bool isChii) {
      // The claimed tile is the meld member NOT in our hand; the others are ours.
      // Removing our contributed tiles and adding the meld must lower shanten.
      var claimed = calledTiles.FirstOrDefault(t => !t.IsTsumo) ?? calledTiles[0];
      var fromHand = calledTiles.Where(t => !ReferenceEquals(t, claimed)).ToList();

      int before = SelfBestShanten(view);

      var remaining = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        remaining.Add(view.SelfHand.pendingTile);
      }
      foreach (var t in fromHand) {
        int at = remaining.FindIndex(h => h.tile.IsSame(t.tile));
        if (at < 0) {
          return 0; // we do not actually hold the tiles - should not happen
        }
        remaining.RemoveAt(at);
      }
      var meld = isChii ? (MenLike)new Shun(calledTiles) : new Kou(calledTiles);
      var called = new List<MenLike>(view.SelfCalled) { meld };
      int after = view.ShantenOf(remaining, called);

      // Must strictly advance the hand to justify giving up menzen.
      if (after >= before) {
        return 0;
      }

      // The open hand must be able to finish with a yaku. Without a guaranteed
      // yaku source, an open hand often cannot win at all - decline.
      if (!OpenHandCanHaveYaku(view, calledTiles, called, remaining, isChii)) {
        return 0;
      }

      double score = 100; // base value of advancing toward an open win
      score += (before - after) * 40; // reward bigger shanten jumps
      score += calledTiles.Sum(t => view.CountDora(t.tile) + (t.tile.Akadora ? 1 : 0)) * 10;
      if (after == 0) {
        score += 30; // calling directly into tenpai is especially strong
      }
      return score;
    }

    /// <summary>
    /// Whether an open hand built with the given meld can plausibly complete with
    /// a yaku (a hard requirement to win open). Recognizes the fast, reliable
    /// open yaku: yakuhai, tanyao, and full flushes (honitsu / chinitsu).
    /// </summary>
    private static bool OpenHandCanHaveYaku(
        PublicGameView view, IReadOnlyList<GameTile> calledTiles,
        IReadOnlyList<MenLike> allCalled, IReadOnlyList<GameTile> remaining, bool isChii) {
      // Yakuhai: ponning a value honor guarantees a yaku outright.
      if (!isChii && IsValueHonor(view, calledTiles[0].tile)) {
        return true;
      }

      // Gather every tile the finished hand would contain (concealed + all melds).
      var all = new List<Tile>();
      foreach (var t in remaining) {
        all.Add(t.tile);
      }
      foreach (var meld in allCalled) {
        foreach (var t in meld) {
          all.Add(t.tile);
        }
      }

      // Tanyao: no terminals or honors anywhere.
      if (all.All(t => t.IsMPS && t.Num is >= 2 and <= 8)) {
        return true;
      }

      // Honitsu / chinitsu: a single suit (+ honors for honitsu).
      var suits = all.Where(t => t.IsMPS).Select(t => t.Suit).Distinct().ToList();
      if (suits.Count <= 1) {
        return true;
      }

      // An already-open hand that had a yaku source keeps it; but with mixed suits
      // and no honor/tanyao path, opening further is not clearly winnable.
      return false;
    }

    /// <summary>
    /// Scores a daiminkan (open kan on a discard). Like a yakuhai pon it breaks
    /// menzen, so we only take it for a value honor that advances the hand; the
    /// extra dora + rinshan make it slightly better than the equivalent pon.
    /// </summary>
    private static void EvaluateDaiminkan(
        PublicGameView view, IReadOnlyList<ChooseTilesActionOption> options,
        ref double bestScore, ref int bestActionIdx, ref int bestOptionIdx, int actionIndex) {
      // Do not open-kan while folding.
      if (view.OpponentSeats.Any(view.IsRiichi) && SelfBestShanten(view) > 0) {
        return;
      }
      for (int j = 0; j < options.Count; j++) {
        var tiles = options[j].tiles;
        if (tiles.Count == 0 || !IsValueHonor(view, tiles[0].tile)) {
          continue;
        }
        double score = 90
            + tiles.Sum(t => view.CountDora(t.tile) + (t.tile.Akadora ? 1 : 0)) * 10;
        if (score > bestScore) {
          bestScore = score;
          bestActionIdx = actionIndex;
          bestOptionIdx = j;
        }
      }
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
