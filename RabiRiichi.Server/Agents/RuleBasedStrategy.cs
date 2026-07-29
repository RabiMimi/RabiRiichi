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
  ///
  /// The strategy is built around a single idea: every option is priced in
  /// POINTS. An option's score is
  ///
  ///   (chance this hand still wins) x (what it pays) - (what discarding this
  ///   tile is expected to cost us in deal-ins)
  ///
  /// which makes push/fold, efficiency, hand value and safety fall out of one
  /// comparison instead of needing hand-tuned weights that fight each other.
  ///
  /// Two engines feed that comparison:
  ///  - <see cref="HandPlan"/> answers "which yaku can this shape still reach",
  ///    which is what makes the agent respect the table's minimum-han rule. A
  ///    hand with no route to <c>minHan</c> yaku han cannot win at all, so it is
  ///    priced at (almost) zero and the agent stops feeding it tiles.
  ///  - <see cref="DefenseModel"/> reads the table for threats and prices each
  ///    tile's deal-in risk from public evidence only.
  /// </summary>
  public static class RuleBasedStrategy {
    /// <summary>
    /// Chooses a response for the given inquiry. Actions are considered in
    /// descending value: winning &gt; riichi &gt; set-asides &gt; discard &gt;
    /// call &gt; skip.
    /// </summary>
    public static InquiryResponse Decide(PublicGameView view, SinglePlayerInquiry inquiry) {
      var actions = inquiry.actions;
      var ctx = TurnContext.Build(view);

      // 1. Always take a valid win. The engine only offers ron/tsumo when the
      //    hand is legal under the min-han rule, so this is never a trap.
      int agariIdx = actions.FindIndex(a => a is AgariAction);
      if (agariIdx >= 0) {
        return Confirm(view.Seat, agariIdx);
      }

      // 2. Abortive draw (nine terminals). Worth taking with a hopeless hand,
      //    but a nine-kinds hand is also a live thirteen-orphans start.
      int ryuukyokuIdx = actions.FindIndex(a => a is RyuukyokuAction);
      if (ryuukyokuIdx >= 0 && ShouldDeclareAbortiveDraw(ctx)) {
        return Confirm(view.Seat, ryuukyokuIdx);
      }

      // 3. Riichi: prefer declaring when it is worthwhile.
      int riichiIdx = actions.FindIndex(a => a is RiichiAction);
      if (riichiIdx >= 0 && actions[riichiIdx] is RiichiAction riichi
          && ShouldRiichi(ctx, riichi)) {
        int tileIdx = ChooseDiscardIndex(ctx, riichi);
        return Choose(view.Seat, riichiIdx, tileIdx);
      }

      // 4. Self-turn set-asides that compete with the discard: kan (ankan/kakan)
      //    and nukidora. These are only worth taking when they clearly help, so
      //    they are evaluated BEFORE the plain discard - otherwise the AI would
      //    always fall through to discarding and never kan/nuki on its own turn.
      var selfCall = ChooseSelfTurnCall(ctx, actions);
      if (selfCall != null) {
        return selfCall;
      }

      // 5. Plain discard (our own turn).
      int discardIdx = actions.FindIndex(a => a is PlayTileAction and not RiichiAction);
      if (discardIdx >= 0 && actions[discardIdx] is PlayTileAction play) {
        int tileIdx = ChooseDiscardIndex(ctx, play);
        return Choose(view.Seat, discardIdx, tileIdx);
      }

      // 6. Reactive calls on another player's discard (chii/pon/daiminkan) - only
      //    when they clearly advance a hand that can still legally win.
      var reactiveCall = ChooseReactiveCall(ctx, actions);
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

    #region Turn context

    /// <summary>
    /// Everything about the current position that several decisions need, worked
    /// out once per inquiry. Threat detection walks every river and builds safe
    /// tile sets, so it must not be redone per discard candidate.
    /// </summary>
    internal sealed class TurnContext {
      public PublicGameView View { get; private init; }
      public IReadOnlyList<SeatThreat> Threats { get; private init; }

      /// <summary> Our own tiles this turn (free tiles plus any pending draw). </summary>
      public List<GameTile> Hand { get; private init; }

      /// <summary> Shanten of the best 13-tile hand we can hold right now. </summary>
      public int Shanten { get; private init; }

      /// <summary> Draws we personally still expect to take this hand. </summary>
      public int DrawsLeft { get; private init; }

      /// <summary>
      /// Shanten + acceptance per candidate discard. Each entry costs a full
      /// resolver pass, and the riichi check, the discard choice and the kan
      /// wait-damage check all want the same fourteen answers, so they are
      /// computed once per inquiry.
      /// </summary>
      private readonly Dictionary<GameTile, DiscardEval> evalCache = new();

      /// <summary> Memoized <see cref="ChooseDiscardIndex"/> per action. </summary>
      private readonly Dictionary<PlayTileAction, int> discardChoiceCache = new();

      /// <summary>
      /// Shanten + acceptance for a candidate discard, searched only as far as
      /// it could matter.
      ///
      /// <see cref="Shanten"/> is already the best any discard can reach, so a
      /// discard more than one worse is never the efficient choice; capping the
      /// search there is what keeps a turn affordable. A capped-out option is
      /// not removed from consideration - it reports an unreachable shanten and
      /// is then ranked purely on safety, which is exactly what is wanted when
      /// the hand is folding.
      /// </summary>
      public DiscardEval Evaluate(GameTile discard) {
        if (!evalCache.TryGetValue(discard, out var eval)) {
          int cap = Shanten == int.MaxValue ? int.MaxValue : Shanten + 1;
          eval = View.EvaluateDiscard(discard, Hand, cap);
          evalCache[discard] = eval;
        }
        return eval;
      }

      public int ChoiceFor(PlayTileAction action, System.Func<int> compute) {
        if (!discardChoiceCache.TryGetValue(action, out var index)) {
          index = compute();
          discardChoiceCache[action] = index;
        }
        return index;
      }

      public static TurnContext Build(PublicGameView view) {
        var hand = new List<GameTile>(view.SelfHand.freeTiles);
        if (view.SelfHand.pendingTile != null) {
          hand.Add(view.SelfHand.pendingTile);
        }
        return new TurnContext {
          View = view,
          Threats = DefenseModel.Threats(view),
          Hand = hand,
          Shanten = view.ShantenOf(hand),
          DrawsLeft = view.WallRemaining / System.Math.Max(1, view.PlayerCount),
        };
      }

      public bool AnyThreat => Threats.Count > 0;
      public bool AnyRiichi => Threats.Any(t => t.Riichi);

      /// <summary>
      /// Combined pressure from the table, in points at risk. A single mildly
      /// suspicious opponent is worth a couple of hundred; a riichi is worth
      /// thousands, which is what separates "stay alert" from "fold".
      /// </summary>
      public double ThreatPressure =>
          Threats.Sum(t => t.TenpaiOdds * t.ExpectedValue);

      /// <summary>
      /// Whether the table is dangerous enough that we should stop committing to
      /// our own hand. A declared riichi always qualifies; otherwise it takes
      /// real accumulated evidence, not one opponent who is merely a few turns in.
      /// </summary>
      public bool UnderRealPressure => AnyRiichi || ThreatPressure >= 1200;
    }

    #endregion

    #region Valuation

    /// <summary>
    /// Chance a hand at this shanten still completes before the wall runs out.
    /// Calibrated to the usual riichi tables: a tenpai hand wins roughly half the
    /// time from an early turn, and each extra shanten costs slightly more than a
    /// factor of two.
    /// </summary>
    private static double WinProbability(
        TurnContext ctx, int shanten, int ukeire, bool furiten) {
      if (shanten < 0) return 1;
      if (shanten >= 9) return 0;

      double p = shanten switch {
        0 => 0.470,
        1 => 0.230,
        2 => 0.105,
        3 => 0.045,
        4 => 0.018,
        5 => 0.007,
        _ => 0.002,
      };

      // A short wall hurts a slow hand far more than a fast one.
      double time = System.Math.Clamp(ctx.DrawsLeft / 12.0, 0.02, 1.0);
      p *= System.Math.Pow(time, 0.5 * (shanten + 1));

      // Wide acceptance completes more often than a narrow shape at the same
      // shanten; 16 live tiles is roughly an average good two-sided wait.
      p *= 0.55 + 0.45 * System.Math.Clamp(ukeire / 16.0, 0, 1);

      // Furiten forbids the ron half of our outs.
      if (furiten) p *= 0.55;

      // Somebody else winning first ends the hand for us too.
      foreach (var threat in ctx.Threats) {
        p *= 1 - 0.18 * threat.TenpaiOdds;
      }
      return System.Math.Clamp(p, 0, 1);
    }

    /// <summary> Rough payout of a finished hand at a given total han. </summary>
    internal static double HanToPoints(int han, bool dealer) {
      double points = han switch {
        <= 0 => 0,
        1 => 1000,
        2 => 2000,
        3 => 3900,
        4 => 7700,
        5 => 8000,
        6 or 7 => 12000,
        8 or 9 or 10 => 16000,
        11 or 12 => 24000,
        _ => 32000,
      };
      return dealer ? points * 1.5 : points;
    }

    /// <summary>
    /// Multiplier for the luck han a concealed hand collects on top of its
    /// counted yaku: ippatsu and uradora once riichi is declared, and the menzen
    /// tsumo half of the outs.
    ///
    /// Riichi and menzen tsumo themselves are ordinary routes in
    /// <see cref="HandPlan"/>, so they must NOT be added again here.
    /// </summary>
    private static double ConcealedUplift(PublicGameView view) {
      if (view.SelfRiichi) return 1.30; // ippatsu is gone, uradora is not
      return view.SelfMenzen ? 1.55 : 1.0;
    }

    /// <summary>
    /// Prices a hypothetical holding using the yaku planner: what it can still
    /// legally win with, times how likely it is to get there.
    ///
    /// A holding with no route to <c>minHan</c> is worth almost nothing no matter
    /// how efficient it is - that is the whole point of the min-han rule - but it
    /// keeps a small residual that shrinks with how far the nearest legal route
    /// is, so the agent still prefers the discard that reopens one.
    /// </summary>
    private static double PlanValue(
        TurnContext ctx, IReadOnlyList<GameTile> freeTiles,
        IReadOnlyList<MenLike> called, int shanten, int ukeire, bool furiten) {
      var view = ctx.View;
      if (shanten == int.MaxValue) return 0;

      var plan = HandPlan.Analyze(view, freeTiles, called);
      bool open = !plan.Menzen;
      int budget = HandPlan.BudgetFor(view, shanten);
      double winChance = WinProbability(ctx, shanten, ukeire, furiten);

      int yakuHan = plan.BestYakuHan(open, budget);
      if (yakuHan < view.MinHan) {
        int reach = plan.DistanceToMinHan(view, open);
        if (reach == int.MaxValue) {
          return 0;
        }
        double revive = 0.30 / (1 + reach);
        return winChance * revive *
            HanToPoints(view.MinHan + plan.BonusHan, view.SelfIsDealer);
      }

      int valueHan = plan.EstimatedValueHan(open, budget);
      return winChance * HanToPoints(valueHan, view.SelfIsDealer)
          * ConcealedUplift(view);
    }

    /// <summary>
    /// Prices a tenpai holding exactly, from the engine's per-wait scoring rather
    /// than the planner's structural guess. Waits that cannot satisfy the table
    /// minimum, and waits with no live copies left, are excluded - a hand whose
    /// only wait is illegal or dead is not really tenpai at all.
    /// </summary>
    private static double TenpaiValue(
        TurnContext ctx, IReadOnlyList<TenpaiInfo> infos, bool furiten, int riichiHan,
        out int liveTiles) {
      var view = ctx.View;
      liveTiles = 0;
      double best = 0;
      foreach (var info in infos) {
        int unseen = view.UnseenCount(info.winningTile);
        if (unseen <= 0) continue;
        int yaku = info.yakuman > 0 ? 13 * info.yakuman : info.yaku + riichiHan;
        if (yaku < view.MinHan) continue;
        liveTiles += unseen;
        // TenpaiInfo.points is the base score (基本点); a ron pays 4x that, 6x
        // for the dealer.
        double value = info.points * (view.SelfIsDealer ? 6 : 4);
        if (value > best) best = value;
      }
      if (liveTiles == 0) return 0;
      // info.points is the plain ron value, so a concealed hand still has its
      // riichi/ippatsu/uradora/tsumo upside ahead of it.
      return WinProbability(ctx, 0, liveTiles, furiten) * best * ConcealedUplift(view);
    }

    #endregion

    #region Discard selection

    /// <summary>
    /// Chooses which option index of a discard action to play, balancing what the
    /// resulting hand is worth against what throwing that tile is likely to cost.
    /// </summary>
    public static int ChooseDiscardIndex(PublicGameView view, PlayTileAction action) =>
        ChooseDiscardIndex(TurnContext.Build(view), action);

    internal static int ChooseDiscardIndex(TurnContext ctx, PlayTileAction action) {
      var options = action.options;
      if (options.Count == 0) {
        return 0;
      }
      return ctx.ChoiceFor(action, () => {
        int bestIdx = 0;
        double bestScore = double.NegativeInfinity;
        for (int i = 0; i < options.Count; i++) {
          double score = ScoreDiscard(ctx, action, options[i].tile);
          if (score > bestScore) {
            bestScore = score;
            bestIdx = i;
          }
        }
        return bestIdx;
      });
    }

    /// <summary>
    /// Higher is a better tile to DISCARD: the value of the hand we are left with
    /// minus the expected cost of putting this particular tile on the table.
    /// </summary>
    private static double ScoreDiscard(
        TurnContext ctx, PlayTileAction action, GameTile tile) {
      var view = ctx.View;
      var eval = ctx.Evaluate(tile);
      var remaining = ctx.Hand.Where(t => !ReferenceEquals(t, tile)).ToList();
      if (eval.Shanten == int.MaxValue) {
        // Too far behind the best discard to be worth pursuing (or an unusable
        // holding). It is still a legal tile, so rank it on safety alone rather
        // than excluding it - that is what makes a fold pick the safest tile.
        return -DefenseModel.ExpectedLoss(view, ctx.Threats, tile.tile)
            + 0.5 * IsolationBonus(view, tile.tile, remaining);
      }

      // Riichi contributes a yaku han that is ours for the taking at tenpai, so
      // it counts toward min-han legality for a hand that is still concealed.
      int riichiHan = !view.SelfRiichi && view.SelfMenzen ? 1 : 0;

      // Prefer the engine's precomputed per-discard scoring, but never depend on
      // it: callers may hand us an action whose candidates were built for a
      // slightly different holding, and a stale wait list would silently mislead
      // every decision below. Recompute whenever it does not describe a tenpai.
      var candidate = action.candidates?.Find(c => c.tile.tile.IsSame(tile.tile));
      var tenpaiInfos = candidate?.tenpaiInfos;
      if (eval.Shanten == 0 && (tenpaiInfos == null || tenpaiInfos.Count == 0)) {
        tenpaiInfos = [.. view.TenpaiInfosOf(remaining)];
      }
      bool furiten = WouldBeFuriten(view, tenpaiInfos, tile);

      double value;
      if (eval.Shanten == 0 && tenpaiInfos is { Count: > 0 }) {
        value = TenpaiValue(ctx, tenpaiInfos, furiten, riichiHan, out int live);
        if (live == 0) {
          // Tenpai on a dead or illegal wait: treat it as a shape that still has
          // to be rebuilt rather than as a finished hand.
          value = 0.6 * PlanValue(
              ctx, remaining, view.SelfCalled, 1, eval.Ukeire, furiten);
        }
      } else {
        value = PlanValue(
            ctx, remaining, view.SelfCalled, eval.Shanten, eval.Ukeire, furiten);
      }

      // Danger. A hand that is still far away will have to survive many more
      // discards, so a risky tile now is a down payment on a risky sequence.
      double risk = DefenseModel.ExpectedLoss(view, ctx.Threats, tile.tile)
          * (1 + 0.5 * System.Math.Max(0, eval.Shanten));

      // Small, deterministic tie-breakers for otherwise equal shapes.
      double tieBreak = 0.5 * IsolationBonus(view, tile.tile, remaining)
          - 2.0 * (view.CountDora(tile.tile) + (tile.tile.Akadora ? 1 : 0));

      return value - risk + tieBreak;
    }

    /// <summary>
    /// Whether discarding this tile leaves us unable to ron: either the tile
    /// itself is one of the resulting waits, or one already sits in our river.
    /// </summary>
    private static bool WouldBeFuriten(
        PublicGameView view, IReadOnlyList<TenpaiInfo> tenpaiInfos, GameTile tile) {
      if (view.SelfFuriten) return true;
      if (tenpaiInfos == null || tenpaiInfos.Count == 0) return false;
      foreach (var info in tenpaiInfos) {
        if (info.winningTile.IsSame(tile.tile)) return true;
        if (view.SelfDiscards.Any(d => d.tile.IsSame(info.winningTile))) return true;
      }
      return false;
    }

    /// <summary>
    /// How little a tile contributes to the remaining shape. An isolated terminal
    /// is the classic "throw this first" tile; a tile with neighbours is not.
    /// </summary>
    private static double IsolationBonus(
        PublicGameView view, Tile tile, IReadOnlyList<GameTile> remaining) {
      if (tile.IsZ) {
        int copies = remaining.Count(t => t.tile.IsSame(tile));
        return copies == 0 ? 3 : 0;
      }
      bool NearBy(int delta) {
        int n = tile.Num + delta;
        return n is >= 1 and <= 9
            && remaining.Any(t => t.tile.Suit == tile.Suit && t.tile.Num == n);
      }
      if (NearBy(0)) return 0;
      if (NearBy(-1) || NearBy(1)) return 0.5;
      if (NearBy(-2) || NearBy(2)) return 1.5;
      return tile.Num is 1 or 9 ? 3 : 2;
    }

    #endregion

    #region Safety (public helper)

    /// <summary>
    /// A rough danger estimate against riichi opponents only: higher = safer to
    /// discard, 0 when nobody has declared. Genbutsu is fully safe; suji, walled
    /// and low-count tiles are safer; live central tiles are the most dangerous.
    ///
    /// This is the narrow, public-facing view of <see cref="DefenseModel"/>; the
    /// strategy itself prices danger in points and also accounts for opponents
    /// who are merely suspected of being tenpai.
    /// </summary>
    public static double SafetyScore(PublicGameView view, Tile tile) {
      var threats = DefenseModel.RiichiThreats(view);
      if (threats.Count == 0) {
        return 0;
      }
      double worst = threats.Max(t => DefenseModel.DealInChance(view, t, tile));
      return System.Math.Clamp(10 - 100 * worst, 0, 10);
    }

    #endregion

    #region Riichi decision

    /// <summary>
    /// Declare riichi when it is the best way to convert this tenpai into points.
    /// Riichi is right far more often than not, so this asks "is there a concrete
    /// reason NOT to" rather than looking for a reason to.
    /// </summary>
    public static bool ShouldRiichi(PublicGameView view, RiichiAction action) =>
        ShouldRiichi(TurnContext.Build(view), action);

    internal static bool ShouldRiichi(TurnContext ctx, RiichiAction action) {
      var view = ctx.View;

      // Not worth locking the hand with no draws left to use it.
      if (view.WallRemaining < 4) {
        return false;
      }

      // Pick the discard we would riichi on and inspect the wait it leaves.
      int idx = ChooseDiscardIndex(ctx, action);
      var candidate = action.candidates?.Find(
          c => idx < action.options.Count && c.tile.tile.IsSame(action.options[idx].tile.tile));
      if (candidate == null || candidate.tenpaiInfos.Count == 0) {
        // No tenpai info: fall back to declaring (the action is only offered when
        // tenpai). At high min-han, do not lock a hand whose legality is unknown.
        return view.MinHan <= 2;
      }

      var liveInfos = candidate.tenpaiInfos
          .Where(t => view.UnseenCount(t.winningTile) > 0)
          .ToList();
      if (liveInfos.Count == 0) {
        return false;
      }

      // Riichi itself contributes one yaku han.
      int bestDeficit = liveInfos.Min(t => YakuHanDeficit(view, t, riichiHan: 1));
      if (bestDeficit >= 2) {
        // Even riichi cannot make this hand legal; stay flexible and keep looking
        // for more yaku instead of freezing the shape.
        return false;
      }

      int legalLive = liveInfos
          .Where(t => YakuHanDeficit(view, t, riichiHan: 1) == 0)
          .Sum(t => view.UnseenCount(t.winningTile));

      if (bestDeficit == 1) {
        // One han short even with riichi: the hand can only be completed by self
        // draw (menzen tsumo) or a lucky ippatsu/haitei. Freezing the shape for
        // that is only worth it when the draw itself is likely - a wide wait, or
        // so late that reshaping is no longer realistic.
        int tsumoOuts = liveInfos.Sum(t => view.UnseenCount(t.winningTile));
        return tsumoOuts >= 8 || view.SelfJun >= 10;
      }

      if (legalLive == 0) {
        return false;
      }

      // A big lead in the last round is better protected than pressed: locking
      // ourselves into pushing every tile risks handing the lead away.
      if (view.IsAllLast && view.SelfRank == 1 && view.SelfPointLead >= 8000
          && view.WallRemaining <= 40) {
        return false;
      }

      // Damaten case: a narrow, cheap wait declared late while the table is
      // already dangerous is a losing proposition - riichi removes our ability
      // to fold without buying much.
      bool cheap = liveInfos.Max(t => t.maxHan) <= 2;
      if (legalLive <= 2 && cheap && view.SelfJun >= 11 && ctx.AnyThreat) {
        return false;
      }

      // Already huge and already legal without riichi: staying silent keeps the
      // fold option and does not scare opponents off our winning tile.
      bool legalWithoutRiichi = liveInfos.Any(
          t => YakuHanDeficit(view, t, riichiHan: 0) == 0);
      if (legalWithoutRiichi && liveInfos.Max(t => t.maxHan) >= 11 && legalLive <= 3) {
        return false;
      }

      return true;
    }

    /// <summary> Yaku han still missing for this wait to be a legal win. </summary>
    private static int YakuHanDeficit(
        PublicGameView view, TenpaiInfo info, int riichiHan = 0) {
      if (info.yakuman > 0) return 0;
      return System.Math.Max(0, view.MinHan - info.yaku - riichiHan);
    }

    #endregion

    #region Abortive draw

    /// <summary>
    /// Nine different terminals/honors on the first turn. Aborting is usually
    /// right, but the very same shape is the start of a thirteen orphans, and a
    /// yakuman beats a redeal - so keep going when the hand is genuinely close.
    /// </summary>
    private static bool ShouldDeclareAbortiveDraw(TurnContext ctx) {
      var view = ctx.View;
      var plan = HandPlan.Analyze(view, ctx.Hand);
      var kokushi = plan.Routes.FirstOrDefault(r => r.Tag == YakuTag.Kokushi);
      if (kokushi != null && kokushi.Distance <= 3) {
        return false;
      }
      // Dealer keeps its seat by aborting, so it is even happier to redeal.
      return true;
    }

    #endregion

    #region Call decision - self turn (kan / nukidora)

    private enum KanKind { Ankan, Kakan, Daiminkan }

    /// <summary>
    /// Classifies a <see cref="KanAction"/> option by matching it against the
    /// viewer's public state: an option whose 3 base tiles already form a called
    /// pon is a kakan; an option containing a tile taken from a discard is a
    /// daiminkan; otherwise it is a concealed ankan.
    /// </summary>
    private static KanKind ClassifyKan(PublicGameView view, IReadOnlyList<GameTile> option) {
      if (option.Count > 0
          && view.SelfCalled.Any(m => m is Kou && m.IsSame(MenLikeOf(option[0])))) {
        return KanKind.Kakan;
      }
      if (option.Any(t => !t.IsTsumo)) {
        return KanKind.Daiminkan;
      }
      return KanKind.Ankan;
    }

    // A throwaway single-tile Kou-key wrapper so we can reuse MenLike.IsSame for
    // "same kind" comparison against a called pon.
    private static Kou MenLikeOf(GameTile tile) => new([tile, tile, tile]);

    /// <summary> First index matching the predicate, or -1. </summary>
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
    /// the caller proceeds to the normal discard.
    /// </summary>
    private static InquiryResponse ChooseSelfTurnCall(
        TurnContext ctx, IReadOnlyList<IPlayerAction> actions) {
      // Nukidora (3-player North): a free bonus han that does not break
      // concealment and does not reveal a dora for anyone else. Always take it.
      int nukiIdx = IndexOfAction(actions, a => a is NukiDoraAction);
      if (nukiIdx >= 0 && actions[nukiIdx] is NukiDoraAction nuki && nuki.options.Count > 0) {
        return Choose(ctx.View.Seat, nukiIdx, 0);
      }

      int kanIdx = IndexOfAction(actions, a => a is KanAction);
      if (kanIdx < 0 || actions[kanIdx] is not KanAction kan || kan.options.Count == 0) {
        return null;
      }

      int bestOption = ChooseSelfKanOption(ctx, kan);
      return bestOption >= 0 ? Choose(ctx.View.Seat, kanIdx, bestOption) : null;
    }

    /// <summary>
    /// Picks which kan option to declare on our own turn, or -1 to decline.
    /// </summary>
    private static int ChooseSelfKanOption(TurnContext ctx, KanAction kan) {
      int bestIdx = -1;
      double bestScore = 0;
      for (int i = 0; i < kan.options.Count; i++) {
        var option = kan.options[i].tiles;
        if (option.Count == 0) {
          continue;
        }
        double score = ScoreKan(ctx, option, ClassifyKan(ctx.View, option));
        if (score > bestScore) {
          bestScore = score;
          bestIdx = i;
        }
      }
      return bestIdx;
    }

    /// <summary>
    /// Net points a kan is worth; 0 or below means decline.
    ///
    /// A kan is not free. It permanently freezes four tiles, which can destroy
    /// pinfu, seven pairs or the very shape that carried the hand's only yaku -
    /// and on a min-han table a hand that loses its yaku route cannot win at all,
    /// so the extra dora would be worth exactly nothing. It also flips a brand
    /// new dora indicator face up for EVERY player, which is a direct gift to the
    /// opponent most likely to be tenpai. So the checks run in this order:
    ///
    ///  1. the shape must not get worse, and at tenpai the wait must not shrink;
    ///  2. the hand must still be able to reach the table's minimum yaku han
    ///     AFTER the kan (checked exactly when the result is tenpai);
    ///  3. we must not be folding;
    ///  4. what the new dora is worth to us must beat what it is worth to them.
    /// </summary>
    private static double ScoreKan(
        TurnContext ctx, IReadOnlyList<GameTile> option, KanKind kind) {
      var view = ctx.View;
      if (view.WallRemaining <= 0) {
        return 0;
      }
      if (!TryApplyKan(ctx, option, kind, out var freeAfter, out var calledAfter)) {
        return 0;
      }

      int before = ctx.Shanten;
      int after = view.ShantenOf(freeAfter, calledAfter);
      if (after == int.MaxValue || after > before) {
        return 0; // the kan breaks our shape
      }

      bool menzenAfter = calledAfter.All(m => m.IsClose);
      int riichiHan = !view.SelfRiichi && menzenAfter && view.WallRemaining >= 4 ? 1 : 0;

      // ---- 1. Wait damage -------------------------------------------------
      int ukeireAfter = view.UkeireOf(freeAfter, calledAfter);
      if (after == 0) {
        // An ankan out of a tenpai hand can silently swap our wait for a worse
        // one. The engine forbids that during riichi; before riichi it is on us.
        int ukeireBefore = BestTenpaiUkeire(ctx);
        if (ukeireBefore > 0 && ukeireAfter < ukeireBefore) {
          return 0;
        }
      } else if (ukeireAfter <= 0 && after > 0) {
        return 0;
      }

      // ---- 2. Minimum-han feasibility after the kan -----------------------
      if (after == 0) {
        int liveYaku = view.BestLiveYakuHanOf(freeAfter, calledAfter);
        if (liveYaku < 0 || liveYaku + riichiHan < view.MinHan) {
          return 0; // the resulting tenpai could never be a legal win
        }
      } else {
        var plan = HandPlan.Analyze(view, freeAfter, calledAfter);
        int budget = HandPlan.BudgetFor(view, after);
        if (plan.BestYakuHan(!menzenAfter, budget) + riichiHan < view.MinHan) {
          return 0;
        }
      }

      // ---- 3. Fold gate ----------------------------------------------------
      // Once in riichi we cannot fold anyway, and the engine already validated
      // that the ankan preserves our wait, so a legal kan there is pure upside.
      // Otherwise: committing four tiles and a free dora to a hand that is not
      // even tenpai, while the table is genuinely dangerous, is how a kan turns
      // a small loss into a big one.
      if (!view.SelfRiichi && after > 0 && ctx.UnderRealPressure) {
        return 0;
      }

      // ---- 4. Price the new dora indicator --------------------------------
      double value = KanValue(ctx, option, kind, freeAfter, calledAfter, after,
          ukeireAfter, menzenAfter);
      double cost = KanCost(ctx, kind);
      return value - cost;
    }

    /// <summary> Points a kan adds for us. </summary>
    private static double KanValue(
        TurnContext ctx, IReadOnlyList<GameTile> option, KanKind kind,
        IReadOnlyList<GameTile> freeAfter, IReadOnlyList<MenLike> calledAfter,
        int shantenAfter, int ukeireAfter, bool menzenAfter) {
      var view = ctx.View;
      double win = WinProbability(ctx, shantenAfter, ukeireAfter, view.SelfFuriten);

      var plan = HandPlan.Analyze(view, freeAfter, calledAfter);
      int budget = HandPlan.BudgetFor(view, shantenAfter);
      int han = plan.EstimatedValueHan(!menzenAfter, budget);
      double handValue = HanToPoints(han, view.SelfIsDealer) * ConcealedUplift(view);

      double gain = 0;
      if (view.AllowsKanDora) {
        // A fresh indicator turns roughly one of our 14 tiles into a dora, and
        // the four kan tiles all count - so a kan on a tile that is already dora
        // is worth much more than one on a random tile.
        int ownDora = option.Sum(t => view.CountDora(t.tile) + (t.tile.Akadora ? 1 : 0));
        gain += win * handValue * (0.18 + 0.10 * ownDora);
      }
      if (view.AllowsKanUradora && (view.SelfRiichi || (menzenAfter && shantenAfter == 0))) {
        // An extra uradora indicator only pays out for a riichi hand - ours.
        gain += win * handValue * 0.12;
      }
      // The replacement draw is a free extra chance at our winning tile.
      gain += win * handValue * 0.05;

      // Upgrading an existing pon costs nothing structurally: the tiles were
      // already committed and the hand was already open.
      if (kind == KanKind.Kakan) {
        gain *= 1.15;
      }
      return gain;
    }

    /// <summary> Points a kan hands to the rest of the table. </summary>
    private static double KanCost(TurnContext ctx, KanKind kind) {
      var view = ctx.View;
      double cost = 0;
      if (view.AllowsKanDora) {
        // Every opponent's hand gets the same dora upgrade we do, and the ones
        // that are actually tenpai are the ones that will cash it.
        foreach (var threat in ctx.Threats) {
          cost += threat.TenpaiOdds * threat.ExpectedValue * 0.20;
        }
      }

      switch (kind) {
        case KanKind.Kakan:
          // Chankan: an opponent waiting on this tile robs the kan outright.
          cost += ctx.Threats.Sum(t => t.TenpaiOdds * t.ExpectedValue * 0.04);
          break;
        case KanKind.Daiminkan:
          // Open kan is the worst of all worlds: it opens the hand, gifts a dora
          // and leaves us responsible for the replacement tile. Demand a lot.
          cost += 1200;
          cost += ctx.Threats.Sum(t => t.TenpaiOdds * t.ExpectedValue * 0.15);
          break;
      }

      // A fourth kan by a second player aborts the hand outright.
      if (view.AbortsOnFourKans && view.TotalKanCount >= 3 && view.SeatsWithKan >= 2) {
        cost += 800;
      }
      return cost;
    }

    /// <summary>
    /// The best yaku han a holding can legally win with, accepting either a
    /// 13-tile waiting hand or a 14-tile one that still has to discard (which is
    /// what a chii/pon leaves us with). Returns -1 when no discard reaches a
    /// tenpai with a live wait.
    /// </summary>
    private static int BestLiveYakuHanForHolding(
        PublicGameView view, IReadOnlyList<GameTile> free, IReadOnlyList<MenLike> called) {
      int meldTiles = called.Sum(m => System.Math.Min(3, m.Count));
      if (free.Count + meldTiles == Game.HAND_SIZE) {
        return view.BestLiveYakuHanOf(free, called);
      }
      if (free.Count + meldTiles != Game.HAND_SIZE + 1) {
        return -1;
      }

      int best = -1;
      var seen = new HashSet<byte>();
      for (int i = 0; i < free.Count; i++) {
        if (!seen.Add(free[i].tile.NoDoraVal)) continue;
        var sub = new List<GameTile>(free);
        sub.RemoveAt(i);
        if (view.ShantenOf(sub, called) != 0) continue;
        int yaku = view.BestLiveYakuHanOf(sub, called);
        if (yaku > best) best = yaku;
      }
      return best;
    }

    /// <summary> Live acceptance of our current best tenpai shape, or 0. </summary>
    private static int BestTenpaiUkeire(TurnContext ctx) {
      var view = ctx.View;
      if (ctx.Hand.Count == 0) return 0;
      int best = 0;
      foreach (var tile in ctx.Hand) {
        var eval = ctx.Evaluate(tile);
        if (eval.Shanten == 0 && eval.Ukeire > best) {
          best = eval.Ukeire;
        }
      }
      return best;
    }

    /// <summary>
    /// Works out the holding a kan would leave us with: which tiles stay free and
    /// what the meld set becomes. Returns false when the option does not match
    /// the tiles we actually hold.
    /// </summary>
    private static bool TryApplyKan(
        TurnContext ctx, IReadOnlyList<GameTile> option, KanKind kind,
        out List<GameTile> freeAfter, out List<MenLike> calledAfter) {
      var view = ctx.View;
      var free = new List<GameTile>(ctx.Hand);
      var called = new List<MenLike>(view.SelfCalled);
      freeAfter = free;
      calledAfter = called;

      if (kind == KanKind.Kakan) {
        // Only the fourth tile comes out of the hand; the pon becomes a kan.
        int at = called.FindIndex(m => m is Kou && m.IsSame(MenLikeOf(option[0])));
        if (at < 0) return false;
        var added = option.FirstOrDefault(t => free.Any(h => ReferenceEquals(h, t)))
            ?? option.FirstOrDefault(t => free.Any(h => h.tile.IsSame(t.tile)));
        if (added == null || !RemoveOne(free, added)) return false;
        called[at] = new Kan(option, TileSource.Kakan);
        return true;
      }

      var source = kind == KanKind.Ankan ? TileSource.Ankan : TileSource.Daiminkan;
      foreach (var tile in option) {
        if (kind == KanKind.Daiminkan && !tile.IsTsumo) {
          continue; // the claimed tile was never in our hand
        }
        if (!RemoveOne(free, tile)) return false;
      }
      called.Add(new Kan(option, source));
      return true;
    }

    /// <summary> Removes one matching tile, preferring the exact instance. </summary>
    private static bool RemoveOne(List<GameTile> tiles, GameTile tile) {
      int at = tiles.FindIndex(t => ReferenceEquals(t, tile));
      if (at < 0) at = tiles.FindIndex(t => t.tile == tile.tile);
      if (at < 0) at = tiles.FindIndex(t => t.tile.IsSame(tile.tile));
      if (at < 0) return false;
      tiles.RemoveAt(at);
      return true;
    }

    #endregion

    #region Call decision - reactive (chii / pon / daiminkan)

    /// <summary>
    /// Considers calling on another player's discard. Opening the hand gives up
    /// riichi, menzen tsumo, pinfu and every closed-hand han multiplier, so a
    /// strong player only calls when the resulting OPEN hand can still reach the
    /// table's minimum yaku han and is worth having.
    /// </summary>
    private static InquiryResponse ChooseReactiveCall(
        TurnContext ctx, IReadOnlyList<IPlayerAction> actions) {
      var view = ctx.View;
      // A declared riichi hand is locked; it can never call.
      if (view.SelfRiichi) {
        return null;
      }

      int bestActionIdx = -1;
      int bestOptionIdx = 0;
      double bestScore = 0;
      for (int i = 0; i < actions.Count; i++) {
        switch (actions[i]) {
          case PonAction pon:
            EvaluateCallOptions(ctx, pon.options, CallKind.Pon,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
          case ChiiAction chii:
            EvaluateCallOptions(ctx, chii.options, CallKind.Chii,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
          case KanAction daiminkan:
            EvaluateCallOptions(ctx, daiminkan.options, CallKind.Daiminkan,
                ref bestScore, ref bestActionIdx, ref bestOptionIdx, i);
            break;
        }
      }

      return bestActionIdx >= 0
          ? Choose(view.Seat, bestActionIdx, bestOptionIdx)
          : null;
    }

    private enum CallKind { Chii, Pon, Daiminkan }

    private static void EvaluateCallOptions(
        TurnContext ctx, IReadOnlyList<ChooseTilesActionOption> options, CallKind kind,
        ref double bestScore, ref int bestActionIdx, ref int bestOptionIdx, int actionIndex) {
      for (int j = 0; j < options.Count; j++) {
        var tiles = options[j].tiles;
        if (tiles.Count == 0) {
          continue;
        }
        double score = ScoreOpenCall(ctx, tiles, kind);
        if (score > bestScore) {
          bestScore = score;
          bestActionIdx = actionIndex;
          bestOptionIdx = j;
        }
      }
    }

    /// <summary>
    /// Value of opening with this meld, or 0 to decline.
    ///
    /// The gate that matters most on a min-han table is the second one: an open
    /// hand loses riichi, so its yaku has to come from the tiles themselves. The
    /// planner is queried with a deliberately tight budget, meaning "the yaku has
    /// to be essentially locked in already" - a two-tiles-away straight is not a
    /// reason to break up a concealed hand.
    /// </summary>
    private static double ScoreOpenCall(
        TurnContext ctx, IReadOnlyList<GameTile> calledTiles, CallKind kind) {
      var view = ctx.View;

      // Never open while the table is dangerous and we are nowhere near a win.
      if (ctx.AnyRiichi && ctx.Shanten >= 2) {
        return 0;
      }

      if (!SplitMeld(ctx, calledTiles, out var remaining)) {
        return 0;
      }
      MenLike meld = kind switch {
        CallKind.Chii => new Shun(calledTiles),
        CallKind.Pon => new Kou(calledTiles),
        _ => new Kan(calledTiles, TileSource.Daiminkan),
      };
      var called = new List<MenLike>(view.SelfCalled) { meld };

      int before = ctx.Shanten;
      int after = view.ShantenOf(remaining, called);
      if (after == int.MaxValue) {
        return 0;
      }

      // ---- 1. The call has to actually speed the hand up ------------------
      if (kind == CallKind.Daiminkan) {
        if (after > before) return 0;
      } else if (after >= before) {
        return 0;
      }

      // ---- 2. The open hand has to be able to win at all ------------------
      var plan = HandPlan.Analyze(view, remaining, called);
      int budget = System.Math.Clamp(after, 0, 2);
      int yakuHan = plan.BestYakuHan(open: true, budget);
      if (yakuHan < view.MinHan) {
        return 0;
      }
      // At tenpai we can check exactly instead of trusting the structural guess.
      if (after == 0) {
        int liveYaku = BestLiveYakuHanForHolding(view, remaining, called);
        if (liveYaku < view.MinHan) {
          return 0;
        }
      }

      int valueHan = yakuHan + plan.BonusHan;

      // ---- 3. Is it worth giving up the concealed hand? -------------------
      if (after > 0) {
        // Away from tenpai, only open for a hand that pays. A single cheap yaku
        // is not worth trading riichi, tsumo, pinfu and the closed han bonuses.
        int required = System.Math.Max(after >= 2 ? 3 : 2, view.MinHan + (after >= 2 ? 1 : 0));
        if (valueHan < required) {
          return 0;
        }
      }
      // A concealed hand that is already close and already legal beats a
      // marginally faster open one. An open kan is checked even at tenpai: it
      // would throw away a riichi that is already available.
      bool checkClosedAlternative = after > 0 || kind == CallKind.Daiminkan;
      if (checkClosedAlternative && view.SelfMenzen && before <= 1) {
        var closedPlan = HandPlan.Analyze(view, ctx.Hand, view.SelfCalled);
        int closedBudget = HandPlan.BudgetFor(view, before);
        // BestYakuHan already counts the riichi route for a concealed hand.
        int closedHan = closedPlan.BestYakuHan(open: false, closedBudget)
            + closedPlan.BonusHan;
        if (closedHan >= view.MinHan && closedHan >= valueHan) {
          return 0;
        }
      }

      // ---- 4. Score it -----------------------------------------------------
      int ukeire = view.UkeireOf(remaining, called);
      double value = after == 0
          ? WinProbability(ctx, 0, System.Math.Max(ukeire, 1), view.SelfFuriten)
              * HanToPoints(valueHan, view.SelfIsDealer)
          : PlanValue(ctx, remaining, called, after, ukeire, view.SelfFuriten);

      // An open kan is the same gift to the table as a self-turn one - a fresh
      // dora indicator for every opponent - on top of opening the hand, so it
      // has to clear the same bar.
      if (kind == CallKind.Daiminkan) {
        value -= KanCost(ctx, KanKind.Daiminkan);
      }

      if (value <= 0) {
        return 0;
      }
      // Reward the size of the jump so the best of several legal calls wins.
      return value * (1 + 0.25 * System.Math.Max(0, before - after));
    }

    /// <summary>
    /// Splits a proposed meld into "the tile the opponent discarded" and "our
    /// own tiles", returning the hand we would be left with. Exactly one meld
    /// tile must be absent from our hand for the call to make sense.
    /// </summary>
    private static bool SplitMeld(
        TurnContext ctx, IReadOnlyList<GameTile> meldTiles, out List<GameTile> remaining) {
      remaining = new List<GameTile>(ctx.Hand);
      int unmatched = 0;
      foreach (var tile in meldTiles) {
        if (!RemoveOne(remaining, tile)) {
          unmatched++;
        }
      }
      return unmatched == 1;
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
