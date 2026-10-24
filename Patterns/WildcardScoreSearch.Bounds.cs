using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Numerics;

namespace RabiRiichi.Patterns {
  internal sealed partial class WildcardScoreSearch {
    private struct Partial {
      internal ulong Present, Sequences, Triplets;
      internal UInt128 TripletCopies;
      internal int MeldCount, SequenceCount, ClosedTriplets, Quads, ClosedQuads, MeldFu;
      internal int WindMelds, DragonMelds, Pair, MaxSequenceCopies;
      internal bool EveryGroup19, AllSequenceOrPair;
    }

    private Partial Summarize() {
      var s = new Partial { Pair = -1, EveryGroup19 = true, AllSequenceOrPair = true };
      UInt128 sequences = 0;
      for (int i = 0; i < 34; i++) if (targets[i] != 0 || required[i] != 0) s.Present |= 1UL << i;
      for (int n = 0; n < count; n++) {
        var g = groups[n]; int i = g.TileIndex;
        bool honorOrTerminal = (1UL << i & (TileMasks.Honor | TileMasks.Terminal)) != 0;
        s.EveryGroup19 &= honorOrTerminal || g.Kind == CompactMeldKind.Sequence && i % 9 == 6;
        s.AllSequenceOrPair &= g.Kind is CompactMeldKind.Sequence or CompactMeldKind.Pair;
        if (g.Kind == CompactMeldKind.Pair) s.Pair = i;
        else if (g.Kind != CompactMeldKind.Single) {
          s.MeldCount++;
          if (i is >= 27 and <= 30) s.WindMelds++;
          if (i >= 31) s.DragonMelds++;
        }
        if (g.Kind == CompactMeldKind.Sequence) {
          s.SequenceCount++; s.Sequences |= 1UL << i;
          sequences += (UInt128)1 << (i * 3);
          s.MaxSequenceCopies = Math.Max(s.MaxSequenceCopies, (int)((sequences >> (i * 3)) & 7));
        }
        if (g.Kind is CompactMeldKind.Triplet or CompactMeldKind.Quad) {
          s.Triplets |= 1UL << i; s.TripletCopies += (UInt128)1 << (i * 3);
          if (g.IsClose) s.ClosedTriplets++;
          s.MeldFu += (honorOrTerminal ? 4 : 2) * (g.IsClose ? 2 : 1) * (g.Kind == CompactMeldKind.Quad ? 4 : 1);
          if (g.Kind == CompactMeldKind.Quad) { s.Quads++; if (g.IsClose) s.ClosedQuads++; }
        }
      }
      return s;
    }

    private bool CanPrune(BasePattern family, int remainingMelds) {
      // Built-in bonus-only rules depend solely on physical tiles and context;
      // all their interpretations have the same score and no qualifying yaku.
      // A RuleYaku may be typed Bonus yet depend on the interpretation.
      if ((mask & ~PatternMask.Bonus) == 0 && boundCustom.Length == 0) {
        if (minHan > 0) return true;
        bool allFound = true;
        foreach (var q in queries) allFound &= q.Found;
        if (allFound) return true;
      }
      if (boundPatterns.Length == 0) return minHan > 0;
      var partial = Summarize();
      Span<CompactScore> contextBounds = stackalloc CompactScore[optimisticQueries.Length];
      for (int i = 0; i < optimisticQueries.Length; i++)
        contextBounds[i] = UpperBound(family, partial, remainingMelds, optimisticQueries[i]);
      if (queries.Length > 3) {
        if (floorDirty) {
          hasFloor = true; bool first = true;
          foreach (var q in queries) {
            if (!q.Found) { hasFloor = false; break; }
            if (first || ScoreMath.Compare(q.Best, floor, hand.game.config.scoringOption) < 0) floor = q.Best;
            first = false;
          }
          floorDirty = false;
        }
        if (hasFloor) {
          bool belowEveryBest = true;
          foreach (var optimistic in contextBounds) {
            if (optimistic.IsValid(minHan) && ScoreMath.Compare(optimistic, floor, hand.game.config.scoringOption) > 0) { belowEveryBest = false; break; }
          }
          if (belowEveryBest) return true;
        }
      }
      bool anyActive = false;
      bool trackEveryQuery = (hand.game.config.scoringOption & (ScoringOption.Yakuman | ScoringOption.MultipleYakuman)) == ScoringOption.Yakuman;
      for (int index = 0; index < queries.Length; index++) {
        if (!activeQueries[index]) continue;
        var query = queries[index];
        // A natural winning tile needs an unoccupied slot of its own. This
        // matters especially for preview batches: absent waits must not keep
        // a complete target alive merely because their score bound is high.
        if (family is Base33332 && query.Index >= 0
            && freeTargets[query.Index] + remainingMelds * 3 <= natural[query.Index]) {
          activeQueries[index] = false; continue;
        }
        // A bound includes every registered, nonexcluded pattern, even those
        // reached only as dependencies. Suppression is intentionally ignored:
        // all terms are nonnegative, so this remains an admissible upper bound.
        var upper = contextBounds[query.Context];
        // Physical bonuses vary between waits but have no structural effect.
        var optimisticQuery = optimisticQueries[query.Context];
        int bonusDifference = Bonus(optimisticQuery) - Bonus(query);
        upper = upper with { Han = upper.Han - bonusDifference };
        if (query.Index >= 0 && (partial.Present & (1UL << query.Index)) == 0
            && (!query.Found || ScoreMath.Compare(upper, query.Best, hand.game.config.scoringOption) > 0))
          upper = UpperBound(family, partial, remainingMelds, query);
        if (!upper.IsValid(minHan) || query.Found && ScoreMath.Compare(upper, query.Best, hand.game.config.scoringOption) <= 0) {
          activeQueries[index] = false; continue;
        }
        anyActive = true;
        if (!trackEveryQuery) return false;
      }
      return !anyActive;
    }

    private CompactScore UpperBound(BasePattern family, Partial s, int remaining, Query q) {
      ulong present = s.Present | (q.Index >= 0 ? 1UL << q.Index : 0);
      bool closed = hand.menzen;
      int suits = ((present & 511) != 0 ? 1 : 0) | ((present >> 9 & 511) != 0 ? 2 : 0) | ((present >> 18 & 511) != 0 ? 4 : 0);
      bool oneSuit = BitOperations.PopCount((uint)suits) <= 1;
      bool honors = (present & TileMasks.Honor) != 0;
      bool only19 = (present & ~(TileMasks.Honor | TileMasks.Terminal)) == 0;
      bool allSequence = s.AllSequenceOrPair;
      int closedTrips = s.ClosedTriplets + remaining;
      int han = 0, yakuman = 0, bonusYakuman = 0, fu = 0;
      int concealedYakuman = 0, gatesYakuman = 0, orphansYakuman = 0, helloYakuman = 0;
      int flushHan = 0, reachHan = 0, outsideHan = 0, repeatedSequenceHan = 0;
      int honorFlushHan = 0, honorOutsideHan = 0, simplesHan = 0;
      Span<int> honorWeights = stackalloc int[34]; honorWeights.Clear();
      foreach (var rule in boundRules) {
        bool possible = true; int h = 0, y = 0, by = 0;
        switch (rule) {
          case YakuRule.Fu33332:
            fu += (Math.Max(30, 20 + s.MeldFu + remaining * 8 + 6 + (q.Incoming.IsTsumo ? 2 : closed ? 10 : 0)) + 9) / 10 * 10;
            break;
          case YakuRule.Fu72: case YakuRule.Fu13_1: fu += 25; break;
          case YakuRule.Chankan: possible = q.Incoming.discardInfo?.reason == DiscardReason.Chankan; h = 1; break;
          case YakuRule.HaiteiRaoyue: possible = q.Incoming.IsTsumo && hand.game.wall.IsHaitei && q.Incoming.source == TileSource.Wall; h = 1; break;
          case YakuRule.HouteiRaoyui: possible = !q.Incoming.IsTsumo && hand.game.wall.IsHaitei; h = 1; break;
          case YakuRule.Iipeikou: possible = closed && s.MaxSequenceCopies + remaining >= 2; h = 1; break;
          case YakuRule.Ippatsu: possible = hand.ippatsu; h = 1; break;
          case YakuRule.MenzenchinTsumohou: possible = closed && q.Incoming.IsTsumo; h = 1; break;
          case YakuRule.Pinfu: possible = closed && allSequence && (s.Pair < 0 || !hand.player.IsYaku(CompactMeld.TileAt(s.Pair))); h = 1; if (possible) fu += q.Incoming.IsTsumo ? 20 : 30; break;
          case YakuRule.Riichi: possible = hand.riichi; h = 1; break;
          case YakuRule.RinshanKaihou: possible = q.Incoming.source == TileSource.Wanpai; h = 1; break;
          case YakuRule.Tanyao: possible = (present & (TileMasks.Honor | TileMasks.Terminal)) == 0 && (closed || (hand.game.config.agariOption & AgariOption.Kuitan) != 0); h = 1; break;
          case YakuRule.YakuhaiBakaze: honorWeights[27 + (int)hand.game.info.wind]++; break;
          case YakuRule.YakuhaiJikaze: honorWeights[27 + (int)hand.player.Wind]++; break;
          case YakuRule.YakuhaiChun: honorWeights[33]++; break;
          case YakuRule.YakuhaiHaku: honorWeights[31]++; break;
          case YakuRule.YakuhaiHatsu: honorWeights[32]++; break;
          case YakuRule.Chantaiyao: possible = s.EveryGroup19; h = closed ? 2 : 1; break;
          case YakuRule.Chiitoitsu: h = 2; break;
          case YakuRule.DoubleRiichi: possible = hand.riichi && hand.wRiichi; h = 2; break;
          case YakuRule.Honroutou: possible = s.SequenceCount == 0 && only19; h = 2; break;
          case YakuRule.Ittsu: possible = PossibleSequencePattern(s.Sequences, remaining, true); h = closed ? 2 : 1; break;
          case YakuRule.Sanankou: possible = closedTrips >= 3; h = 2; break;
          case YakuRule.Sankantsu: possible = s.Quads >= 3; h = 2; break;
          case YakuRule.SanshokuDoujun: possible = PossibleSequencePattern(s.Sequences, remaining, false); h = closed ? 2 : 1; break;
          case YakuRule.SanshokuDoukou: possible = PossibleSequencePattern(s.Triplets, remaining, false, 9); h = 2; break;
          case YakuRule.Shousangen: possible = (s.Pair < 0 || s.Pair >= 31) && BitOperations.PopCount(s.Triplets & (7UL << 31)) + remaining >= 2; h = 2; break;
          case YakuRule.Toitoi: possible = s.SequenceCount == 0; h = 2; break;
          case YakuRule.Honitsu: possible = oneSuit; h = closed ? 3 : 2; break;
          case YakuRule.JunchanTaiyao: possible = s.EveryGroup19 && !honors; h = closed ? 3 : 2; break;
          case YakuRule.Ryanpeikou: possible = closed && s.SequenceCount + remaining >= 4 && BitOperations.PopCount(s.Sequences) <= 2; h = 3; break;
          case YakuRule.Chinitsu: possible = oneSuit && !honors; h = closed ? 6 : 5; break;
          case YakuRule.Chiihou: possible = q.Incoming.IsTsumo && hand.game.IsFirstJun && !hand.player.IsDealer; y = 1; break;
          case YakuRule.Chinroutou: possible = (present & ~TileMasks.Terminal) == 0; y = 1; break;
          case YakuRule.ChuurenPoutou: possible = closed && !honors && oneSuit && s.Quads == 0 && FitsSpecial(q, false); y = 1; break;
          case YakuRule.JunseiChuurenPoutou: possible = closed && !honors && oneSuit && s.Quads == 0 && hand.called.Count == 0 && FitsSpecial(q, false); y = 2; break;
          case YakuRule.HelloWorld: possible = closed && !honors && oneSuit && FitsSpecial(q, true); y = 3; break;
          case YakuRule.Daisangen: possible = s.MeldCount - s.DragonMelds <= 1 && BitOperations.PopCount(s.Triplets & (7UL << 31)) + remaining >= 3; y = 1; break;
          case YakuRule.Daisuushii: possible = s.MeldCount == s.WindMelds && BitOperations.PopCount(s.Triplets & (15UL << 27)) + remaining >= 4; y = 2; break;
          case YakuRule.Shousuushii: possible = (s.Pair < 0 || s.Pair is >= 27 and <= 30) && s.MeldCount - s.WindMelds <= 1 && BitOperations.PopCount(s.Triplets & (15UL << 27)) + remaining >= 3; y = 1; break;
          case YakuRule.KokushiMusou: y = 1; break;
          case YakuRule.KokushiMusouJuusanmenMachi: y = 2; break;
          case YakuRule.Ryuuiisou: possible = (present & ~TileMasks.Green) == 0; y = 1; break;
          case YakuRule.Suuankou: possible = s.SequenceCount == 0 && closedTrips >= 4; y = 1; break;
          case YakuRule.SuuankouTanki: possible = s.SequenceCount == 0 && closedTrips >= 4 && (q.Index < 0 || s.Pair < 0 || q.Index == s.Pair); y = 2; break;
          case YakuRule.Suukantsu: possible = s.Quads == 4; y = 1; break;
          case YakuRule.Tenhou: possible = q.Incoming.IsTsumo && hand.jun == 1 && hand.player.IsDealer; y = 1; break;
          case YakuRule.Tsuuiisou: possible = suits == 0; y = 1; break;
          case YakuRule.Benikujaku: possible = (present & ~TileMasks.Red) == 0; y = 1; break;
          case YakuRule.Heiiisou: possible = (present & ~TileMasks.Black) == 0; y = 1; break;
          case YakuRule.Chaopaikou: possible = PossibleKouSeries(s.Triplets, remaining, 3, 2); h = 2; break;
          case YakuRule.Chinpaikou: possible = PossibleKouSeries(s.Triplets, remaining, 3, 3); h = 2; break;
          case YakuRule.Sanrenkou: possible = PossibleKouSeries(s.Triplets, remaining, 3, 1); h = 2; break;
          case YakuRule.Suurenkou: possible = PossibleKouSeries(s.Triplets, remaining, 4, 1); y = 1; break;
          case YakuRule.Chinpeikou:
            possible = closed && s.SequenceCount + remaining >= 4 && BitOperations.PopCount(s.Sequences) <= 2
                && PossibleSameRankSequences(s.Sequences); h = 3; break;
          case YakuRule.Daichiishin: possible = closed && suits == 0; y = 2; break;
          case YakuRule.Daisharin: possible = closed && (present & ~(254UL << 9)) == 0; y = 1; break;
          case YakuRule.Daichikurin: possible = closed && (present & ~(254UL << 18)) == 0; y = 1; break;
          case YakuRule.Daisuurin: possible = closed && (present & ~254UL) == 0; y = 1; break;
          case YakuRule.IshiNoUeNiMoSannen: possible = hand.riichi && hand.wRiichi && hand.game.wall.IsHaitei; y = 1; break;
          case YakuRule.IsshokuSandoujun: possible = s.MaxSequenceCopies + remaining >= 3; h = closed ? 3 : 2; break;
          case YakuRule.IsshokuYondoujun: possible = s.MaxSequenceCopies + remaining >= 4; y = 1; break;
          case YakuRule.Kanburi: possible = q.Incoming.discardInfo?.reason == DiscardReason.DrawRinshan; h = 1; break;
          case YakuRule.Katengecchi: possible = hand.game.wall.IsHaitei && q.Incoming.source == TileSource.Wanpai; y = 1; break;
          case YakuRule.Paarenchan: possible = hand.player.IsDealer && hand.player.agariStreak + 1 >= Paarenchan.REQUIRED_STREAK; by = 1; break;
          case YakuRule.Renhou: possible = !q.Incoming.IsTsumo && !hand.player.IsDealer && hand.jun == 0 && hand.game.IsFirstJun; y = 1; break;
          case YakuRule.Ryanankan: possible = s.ClosedQuads >= 2; h = 2; break;
          case YakuRule.Sanankan: possible = s.ClosedQuads >= 3; y = 1; break;
          case YakuRule.Sanfonkou: possible = s.WindMelds + remaining >= 3; h = 2; break;
          case YakuRule.ShiiaruRaotai: possible = hand.freeTiles.Count == 1; h = 1; break;
          case YakuRule.Shiisanputa: possible = true; y = 1; break;
          case YakuRule.Shiisuuputa: possible = s.Pair < 0; y = 1; break;
          case YakuRule.Shousanfon: possible = (s.Pair < 0 || s.Pair is >= 27 and <= 30) && s.WindMelds + remaining >= 2; h = 1; break;
          case YakuRule.TsubameGaeshi: possible = q.Incoming.discardInfo?.isRiichiDeclaration == true; h = 1; break;
          case YakuRule.Uumensai:
            int missingFamilies = 3 - BitOperations.PopCount((uint)suits)
                + ((present & (15UL << 27)) == 0 ? 1 : 0) + ((present & (7UL << 31)) == 0 ? 1 : 0);
            possible = family is not Base33332 || missingFamilies <= remaining; h = 2; break;
          case YakuRule.Akadora: case YakuRule.Dora: case YakuRule.Uradora: case YakuRule.NukiDora: break;
        }
        if (possible) {
          // These replacement families cannot contribute both scores together.
          if (rule is YakuRule.Suuankou or YakuRule.SuuankouTanki) concealedYakuman = Math.Max(concealedYakuman, y);
          else if (rule is YakuRule.ChuurenPoutou or YakuRule.JunseiChuurenPoutou) gatesYakuman = Math.Max(gatesYakuman, y);
          else if (rule is YakuRule.KokushiMusou or YakuRule.KokushiMusouJuusanmenMachi) orphansYakuman = Math.Max(orphansYakuman, y);
          else if (rule == YakuRule.HelloWorld) helloYakuman = y;
          else yakuman += y;
          if (rule is YakuRule.Honitsu or YakuRule.Chinitsu) flushHan = Math.Max(flushHan, h);
          else if (rule is YakuRule.Riichi or YakuRule.DoubleRiichi) reachHan = Math.Max(reachHan, h);
          else if (rule is YakuRule.Chantaiyao or YakuRule.JunchanTaiyao) outsideHan = Math.Max(outsideHan, h);
          else if (rule is YakuRule.Iipeikou or YakuRule.Ryanpeikou) repeatedSequenceHan = Math.Max(repeatedSequenceHan, h);
          else if (rule == YakuRule.Tanyao) simplesHan += h;
          else han += h;
          if (rule == YakuRule.Honitsu) honorFlushHan = Math.Max(honorFlushHan, h);
          if (rule == YakuRule.Chantaiyao) honorOutsideHan = Math.Max(honorOutsideHan, h);
          bonusYakuman += by;
        }
      }
      han += flushHan + reachHan + outsideHan + repeatedSequenceHan + simplesHan;
      // Nine gates uses nine identities; four triplets plus a pair use at most
      // five. Hello World has a mandatory singleton 8 and cannot be all triplets,
      // and its histogram has no 1, excluding nine gates. These three families
      // are pairwise incompatible even with custom multiplicities.
      yakuman += Math.Max(helloYakuman, Math.Max(concealedYakuman, gatesYakuman)) + orphansYakuman;
      int maxWeight = 0;
      for (int i = 27; i < 34; i++) { han += honorWeights[i] * TripletUpper(s, i, 0); maxWeight = Math.Max(maxWeight, honorWeights[i]); }
      han += remaining * maxWeight;
      // Future honor triplets cannot coexist with a full flush, pure outside
      // hand, or all simples. Bound both alternatives jointly instead of
      // spending the same future meld slots on incompatible han awards.
      if (family is Base33332 && !honors && suits != 0) {
        int futureValueHan = remaining * maxWeight;
        int withoutHonors = flushHan + outsideHan + simplesHan;
        int withHonors = honorFlushHan + honorOutsideHan + futureValueHan;
        han += Math.Max(withoutHonors, withHonors) - (withoutHonors + futureValueHan);
      }
      // A flush needs a suited group. When every mandatory/placed tile is an
      // honor, that group consumes a future meld slot which cannot also be a
      // value-honor triplet. Alternatively an all-honor target has no flush han.
      if (family is Base33332 && suits == 0 && flushHan > 0)
        han -= Math.Min(flushHan, remaining > 0 ? maxWeight : flushHan);
      // A RuleYaku contributes its full (nonnegative) value unless its condition
      // fails in every completion of this target.
      int customBonus = 0;
      if (boundCustom.Length > 0) {
        PreparePartial(family, present, remaining, q);
        foreach (var rule in boundCustom) {
          if (!rule.Condition.MayHold(partialView)) continue;
          var v = rule.Score(hand);
          han += v.Yaku; customBonus += v.Han - v.Yaku; fu += v.Fu;
          yakuman += v.Yakuman; bonusYakuman += v.BonusYakuman;
        }
      }
      // Count only active bonus rules; dependency-only registered bonus rules may
      // also be reached, hence the intentionally broad registered-pattern set.
      return new(han + customBonus + Bonus(q), han, fu, yakuman, bonusYakuman);
    }

    private void PreparePartial(BasePattern family, ulong present, int remainingMelds, Query q) {
      partialView.SetContext(hand, q.Incoming);
      partialView.Set(groups, count, targets, lockedCounts, required, natural, q.Index, present);
      int tiles = 0; bool pair = false;
      for (int n = 0; n < count; n++) { tiles += groups[n].Count; pair |= groups[n].Kind == CompactMeldKind.Pair; }
      switch (family) {
        case Base33332: partialView.SetRemaining(remainingMelds, pair ? 0 : 1, 0, remainingMelds * 3 + (pair ? 0 : 2)); break;
        // Seven-pair and orphan searches pass no meld count; derive capacity from the placed groups.
        case Base72: partialView.SetRemaining(0, 7 - count, 0, (7 - count) * 2); break;
        case Base13_1: partialView.SetRemaining(0, 0, 0, 0); break;
        default:
          int left = Game.HAND_SIZE + 1 - tiles;
          partialView.SetRemaining(0, pair || left < 2 ? 0 : 1, left, left); break;
      }
    }

    private int Bonus(Query q) => q.Dora * doraFactor + q.Aka * akaFactor
        + q.Ura * uraFactor + hand.nukiDora.Count * nukiFactor;

    private static bool PossibleSameRankSequences(ulong mask) {
      if (BitOperations.PopCount(mask) < 2) return true;
      int first = BitOperations.TrailingZeroCount(mask);
      int second = BitOperations.TrailingZeroCount(mask & (mask - 1));
      return first / 9 != second / 9 && first % 9 == second % 9;
    }

    private static int TripletUpper(Partial s, int tile, int remaining) => (int)((s.TripletCopies >> (tile * 3)) & 7) + remaining;

    private static bool PossibleSequencePattern(ulong mask, int remaining, bool ittsu, int ranks = 7) {
      if (ittsu) {
        for (int suit = 0; suit < 3; suit++) if (3 - BitOperations.PopCount((mask >> (suit * 9)) & 73) <= remaining) return true;
      } else for (int rank = 0; rank < ranks; rank++) {
        ulong needed = (1UL << rank) | (1UL << (rank + 9)) | (1UL << (rank + 18));
        if (BitOperations.PopCount(needed & ~mask) <= remaining) return true;
      }
      return false;
    }

    private static bool PossibleKouSeries(ulong mask, int remaining, int length, int step) {
      for (int suit = 0; suit < 3; suit++) for (int start = 0; start + (length - 1) * step < 9; start++) {
        ulong needed = 0;
        for (int n = 0; n < length; n++) needed |= 1UL << (suit * 9 + start + n * step);
        if (BitOperations.PopCount(needed & ~mask) <= remaining) return true;
      }
      return false;
    }

    private bool FitsSpecial(Query q, bool hello) {
      for (int suit = 0; suit < 3; suit++) {
        bool fits = true; int excess = 0;
        for (int i = 0; i < 34; i++) {
          int mandatory = Math.Max(targets[i], Math.Max(required[i], natural[i] + (q.Index == i ? 1 : 0)) + lockedCounts[i]);
          if (i / 9 != suit && mandatory > 0) { fits = false; break; }
          if (i / 9 != suit) continue;
          int rank = i % 9;
          int expected = hello ? rank switch { 1 => 3, 5 => 4, 6 => 3, 7 => 1, 8 => 3, _ => 0 } : rank is 0 or 8 ? 3 : 1;
          if (hello && mandatory > expected) { fits = false; break; }
          if (!hello) excess += Math.Max(0, mandatory - expected);
        }
        if (fits && (hello || excess <= 1)) return true;
      }
      return false;
    }
  }
}
