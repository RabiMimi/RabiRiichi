using RabiRiichi.Core;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// A yaku the hand could plausibly finish with. Tags drive the compatibility
  /// matrix in <see cref="HandPlan"/>; several routes may share a tag (a hand can
  /// hold two different yakuhai triplets, for example).
  /// </summary>
  public enum YakuTag {
    Riichi,
    MenzenTsumo,
    Pinfu,
    Tanyao,
    Yakuhai,
    Honitsu,
    Chinitsu,
    Toitoi,
    Chiitoitsu,
    Chanta,
    Junchan,
    Ittsu,
    SanshokuDoujun,
    Kokushi,
  }

  /// <summary>
  /// One candidate yaku, with how much it is worth and how far away it is.
  ///
  /// <see cref="Distance"/> is a lower bound on the number of tiles currently in
  /// the hand that would still have to be replaced for the yaku to be legal. It
  /// is deliberately a cheap structural estimate, not an exact search: it only
  /// has to be good enough to answer "can this hand realistically still reach the
  /// table's minimum han?".
  /// </summary>
  public sealed class YakuRoute {
    /// <summary> Which yaku this is (drives compatibility). </summary>
    public YakuTag Tag { get; init; }

    /// <summary>
    /// Identity used to de-duplicate variants of the same route (the three
    /// candidate flush suits, say). It must distinguish the separate yakuhai a
    /// hand can hold at once - a dealer's East is BOTH the round wind and the
    /// seat wind and is worth two han, so those must not collapse into one.
    /// </summary>
    public string Key { get; init; }

    /// <summary> Han when the hand finishes concealed. </summary>
    public int ClosedHan { get; init; }

    /// <summary> Han when the hand finishes open; 0 means "closed only". </summary>
    public int OpenHan { get; init; }

    /// <summary> Tiles that still have to be swapped out; lower is closer. </summary>
    public int Distance { get; init; }

    /// <summary>
    /// Whether the yaku is guaranteed once the shape completes. Menzen tsumo only
    /// pays on a self-draw, so it must never be used to justify a hand whose only
    /// winning route is a ron under a minimum-han rule.
    /// </summary>
    public bool Guaranteed { get; init; } = true;

    /// <summary> Han for the requested openness, or 0 when unavailable. </summary>
    public int HanFor(bool open) => open ? OpenHan : ClosedHan;
  }

  /// <summary>
  /// A structural read of what a hand could still become: which yaku are within
  /// reach, and therefore the best han it can legally win with.
  ///
  /// This exists because a minimum-han table (番縛り) makes "advance the hand"
  /// and "win the hand" different goals. A hand that is one tile from tenpai but
  /// has no route to <c>minHan</c> yaku han is worth nothing, so every decision
  /// that permanently narrows the hand's yaku options - calling chii/pon, taking
  /// an ankan that destroys pinfu or chiitoitsu, opening for a single dragon on a
  /// two-han table - has to be checked against this, not just against shanten.
  ///
  /// Everything here is computed from the viewer's own tiles plus public
  /// information, so it is safe for a non-cheating agent.
  /// </summary>
  public sealed class HandPlan {
    /// <summary> Suit index used by the internal counts table. </summary>
    private const int SuitCount = 5; // Invalid, M, P, S, Z

    private readonly int[,] counts = new int[SuitCount, 10];

    /// <summary> Every plausible yaku route, unsorted. </summary>
    public IReadOnlyList<YakuRoute> Routes { get; private set; }

    /// <summary> Whether the analysed holding is still fully concealed. </summary>
    public bool Menzen { get; private set; }

    /// <summary> Total tiles in the analysed holding (kan counted as 3). </summary>
    public int TileCount { get; private set; }

    /// <summary> Dora + akadora + pulled North carried by the holding. </summary>
    public int BonusHan { get; private set; }

    private HandPlan() { }

    /// <summary>
    /// Analyses a holding (free tiles plus melds) in the context of the current
    /// round/seat winds and rule options.
    /// </summary>
    public static HandPlan Analyze(
        PublicGameView view,
        IReadOnlyList<GameTile> freeTiles,
        IReadOnlyList<MenLike> called = null) {
      called ??= view.SelfCalled;
      var plan = new HandPlan {
        Menzen = called.All(meld => meld.IsClose),
      };

      int bonus = view.SelfNukiDoraCount;
      void Track(GameTile tile) {
        var t = tile.tile;
        if (!t.IsValid) return;
        plan.counts[(int)t.Suit, t.Num]++;
        plan.TileCount++;
        bonus += view.CountDora(t) + (t.Akadora ? 1 : 0);
      }

      foreach (var tile in freeTiles) {
        Track(tile);
      }
      foreach (var meld in called) {
        // A kan contributes four physical tiles of value but occupies three
        // "slots" of hand shape; count the shape as three so the structural
        // distances below stay comparable with a 13-tile hand.
        int slot = 0;
        foreach (var tile in meld) {
          if (slot++ < 3) {
            Track(tile);
          } else {
            bonus += view.CountDora(tile.tile) + (tile.tile.Akadora ? 1 : 0);
          }
        }
      }
      plan.BonusHan = bonus;
      plan.Routes = plan.BuildRoutes(view, called);
      return plan;
    }

    #region Route evaluation

    /// <summary>
    /// Best total yaku han (never counting dora) reachable while replacing at
    /// most <paramref name="budget"/> more tiles.
    /// </summary>
    /// <param name="open">Score the open-hand version of each yaku.</param>
    /// <param name="includeSituational">
    /// Include yaku that only pay on some winning routes (menzen tsumo). Leave
    /// false whenever the answer is used to decide whether a hand can legally
    /// win at all, because a ron would not get them.
    /// </param>
    public int BestYakuHan(bool open, int budget, bool includeSituational = false) {
      var feasible = Routes
          .Where(r => r.Distance <= budget && r.HanFor(open) > 0
              && (includeSituational || r.Guaranteed))
          .OrderByDescending(r => r.HanFor(open))
          .ToList();
      if (feasible.Count == 0) {
        return 0;
      }

      // Yaku stack, but not all combinations are legal (tanyao excludes yakuhai,
      // honitsu excludes sanshoku, ...). Anchor on each route in turn and greedily
      // add the highest-value compatible remainder; with a dozen routes this is
      // both exhaustive enough and far cheaper than enumerating every subset.
      int best = 0;
      for (int i = 0; i < feasible.Count; i++) {
        var chosen = new List<YakuRoute> { feasible[i] };
        int total = feasible[i].HanFor(open);
        for (int j = 0; j < feasible.Count; j++) {
          if (j == i) continue;
          var candidate = feasible[j];
          if (chosen.All(c => Compatible(c, candidate))) {
            chosen.Add(candidate);
            total += candidate.HanFor(open);
          }
        }
        if (total > best) {
          best = total;
        }
      }
      return best;
    }

    /// <summary>
    /// Whether the hand can still legally win under the table's minimum-han rule.
    /// </summary>
    public bool CanReachMinHan(PublicGameView view, bool open, int budget) =>
        BestYakuHan(open, budget) >= view.MinHan;

    /// <summary> The cheapest route (in tiles) that alone satisfies min-han. </summary>
    public int DistanceToMinHan(PublicGameView view, bool open) {
      for (int budget = 0; budget <= 14; budget++) {
        if (BestYakuHan(open, budget) >= view.MinHan) {
          return budget;
        }
      }
      return int.MaxValue;
    }

    /// <summary>
    /// A rough value of the finished hand in han, including dora. Used to compare
    /// "is this worth pushing for" rather than for legality.
    /// </summary>
    public int EstimatedValueHan(bool open, int budget) =>
        BestYakuHan(open, budget, includeSituational: true) + BonusHan;

    /// <summary>
    /// How many more tiles a hand at the given shanten can realistically still
    /// swap out: roughly one per remaining draw, capped so that a far-from-tenpai
    /// hand is not credited with an unlimited rebuild.
    /// </summary>
    public static int BudgetFor(PublicGameView view, int shanten) {
      int drawsLeft = view.WallRemaining / System.Math.Max(1, view.PlayerCount);
      int slack = shanten <= 0 ? 0 : shanten + 1;
      return System.Math.Clamp(System.Math.Min(slack, drawsLeft), 0, 8);
    }

    #endregion

    #region Compatibility

    private static bool Compatible(YakuRoute a, YakuRoute b) =>
        Compatible(a.Tag, b.Tag);

    /// <summary>
    /// Whether two yaku can appear in the same finished hand. Symmetric.
    /// Same-tag routes are compatible so a double wind or two dragon triplets
    /// both count.
    /// </summary>
    public static bool Compatible(YakuTag a, YakuTag b) {
      if (a == b) {
        return a == YakuTag.Yakuhai;
      }
      // Order the pair so the table below only needs one direction.
      if (a > b) {
        (a, b) = (b, a);
      }
      // Kokushi is a closed thirteen-orphans hand: nothing else applies.
      if (a == YakuTag.Kokushi || b == YakuTag.Kokushi) {
        return false;
      }
      return (a, b) switch {
        // Pinfu needs four runs and a valueless pair.
        (YakuTag.Pinfu, YakuTag.Yakuhai) => false,
        (YakuTag.Pinfu, YakuTag.Toitoi) => false,
        (YakuTag.Pinfu, YakuTag.Chiitoitsu) => false,

        // All simples excludes anything that requires terminals or honors.
        (YakuTag.Tanyao, YakuTag.Yakuhai) => false,
        (YakuTag.Tanyao, YakuTag.Honitsu) => false,
        (YakuTag.Tanyao, YakuTag.Chanta) => false,
        (YakuTag.Tanyao, YakuTag.Junchan) => false,

        // Yakuhai needs an honor triplet, which no-honor yaku forbid.
        (YakuTag.Yakuhai, YakuTag.Chinitsu) => false,
        (YakuTag.Yakuhai, YakuTag.Junchan) => false,

        // Half flush and full flush are alternatives, not additive.
        (YakuTag.Honitsu, YakuTag.Chinitsu) => false,
        // A half flush keeps honors, so it cannot also be terminals-only.
        (YakuTag.Honitsu, YakuTag.Junchan) => false,
        // Three-colour runs need all three suits.
        (YakuTag.Honitsu, YakuTag.SanshokuDoujun) => false,
        (YakuTag.Chinitsu, YakuTag.SanshokuDoujun) => false,
        // A full flush has no honors, so chanta collapses into junchan.
        (YakuTag.Chinitsu, YakuTag.Chanta) => false,

        // All triplets rules out any run-based yaku and seven pairs.
        (YakuTag.Toitoi, YakuTag.Chiitoitsu) => false,
        (YakuTag.Toitoi, YakuTag.Ittsu) => false,
        (YakuTag.Toitoi, YakuTag.SanshokuDoujun) => false,

        // Seven pairs is its own shape: only suit- and simples-based yaku stack.
        (YakuTag.Chiitoitsu, YakuTag.Chanta) => false,
        (YakuTag.Chiitoitsu, YakuTag.Junchan) => false,
        (YakuTag.Chiitoitsu, YakuTag.Ittsu) => false,
        (YakuTag.Chiitoitsu, YakuTag.SanshokuDoujun) => false,

        // Junchan is the stricter version of chanta; only one is scored.
        (YakuTag.Chanta, YakuTag.Junchan) => false,
        // A straight contains 456, which has neither a terminal nor an honor.
        (YakuTag.Chanta, YakuTag.Ittsu) => false,
        (YakuTag.Junchan, YakuTag.Ittsu) => false,

        _ => true,
      };
    }

    #endregion

    #region Structural distances

    private int Count(TileSuit suit, int num) => counts[(int)suit, num];

    private int SuitTotal(TileSuit suit) {
      int total = 0;
      for (int n = 1; n <= 9; n++) {
        total += counts[(int)suit, n];
      }
      return total;
    }

    private int HonorTotal() => SuitTotal(TileSuit.Z);

    private int TerminalHonorTotal() {
      int total = HonorTotal();
      foreach (var suit in Suits) {
        total += Count(suit, 1) + Count(suit, 9);
      }
      return total;
    }

    private static readonly TileSuit[] Suits =
        [TileSuit.M, TileSuit.P, TileSuit.S];

    /// <summary> Kinds held at least twice / three times, and distinct kinds. </summary>
    private (int pairs, int triplets, int kinds) Shape() {
      int pairs = 0, triplets = 0, kinds = 0;
      for (int s = 1; s < SuitCount; s++) {
        for (int n = 1; n <= 9; n++) {
          int c = counts[s, n];
          if (c <= 0) continue;
          kinds++;
          if (c >= 3) triplets++;
          else if (c == 2) pairs++;
        }
      }
      return (pairs, triplets, kinds);
    }

    private List<YakuRoute> BuildRoutes(PublicGameView view, IReadOnlyList<MenLike> called) {
      var routes = new List<YakuRoute>();
      bool anyOpenMeld = called.Any(m => !m.IsClose);
      bool hasOpenRun = called.Any(m => m is Shun && !m.IsClose);
      bool meldHasYaochuu = called.Any(m => m.Any(t => t.tile.Is19Z));
      bool meldHasSimple = called.Any(m => m.Any(t => !t.tile.Is19Z));

      // ---- Concealed-only bread and butter -------------------------------
      if (!anyOpenMeld) {
        routes.Add(new YakuRoute {
          Tag = YakuTag.Riichi, Key = "riichi", ClosedHan = 1, OpenHan = 0, Distance = 0,
        });
        routes.Add(new YakuRoute {
          Tag = YakuTag.MenzenTsumo, Key = "tsumo", ClosedHan = 1, OpenHan = 0, Distance = 0,
          Guaranteed = false,
        });
      }

      // A concealed kan keeps the hand menzen, but it is still a triplet set
      // aside from four physical tiles: it makes pinfu impossible, and it is
      // neither a pair nor a lone orphan, so seven pairs and thirteen orphans
      // are off the table too. This is precisely how an innocent-looking ankan
      // destroys the only yaku route a hand had - and on a min-han table, a hand
      // with no yaku route cannot win at all.
      if (called.Count == 0) {
        routes.Add(new YakuRoute {
          Tag = YakuTag.Pinfu, Key = "pinfu", ClosedHan = 1, OpenHan = 0, Distance = PinfuDistance(view),
        });
        routes.Add(new YakuRoute {
          Tag = YakuTag.Chiitoitsu, Key = "chiitoitsu", ClosedHan = 2, OpenHan = 0,
          Distance = ChiitoitsuDistance(),
        });
        routes.Add(new YakuRoute {
          Tag = YakuTag.Kokushi, Key = "kokushi", ClosedHan = 13, OpenHan = 0,
          Distance = KokushiDistance(),
        });
      }

      // ---- All simples ---------------------------------------------------
      int tanyaoDistance = meldHasYaochuu ? Unreachable : TerminalHonorTotal();
      routes.Add(new YakuRoute {
        Tag = YakuTag.Tanyao, Key = "tanyao", ClosedHan = 1,
        OpenHan = view.AllowsOpenTanyao ? 1 : 0,
        Distance = tanyaoDistance,
      });

      // ---- Yakuhai (dragons, round wind, seat wind) -----------------------
      // The round wind and the seat wind are separate yaku even when they are
      // the same tile, which is what makes a dealer's East triplet worth two han
      // in an East round - so they get distinct keys and both survive dedup.
      foreach (var (key, honor) in ValueHonorSources(view)) {
        int have = Count(TileSuit.Z, honor);
        routes.Add(new YakuRoute {
          Tag = YakuTag.Yakuhai, Key = key, ClosedHan = 1, OpenHan = 1,
          // Two in hand is a live pon away; a lone honor is a long shot; none at
          // all means the triplet would have to be built from scratch.
          Distance = have >= 3 ? 0 : (3 - have) * 2,
        });
      }

      // ---- Flushes ---------------------------------------------------------
      foreach (var suit in Suits) {
        int inSuit = SuitTotal(suit);
        int honors = HonorTotal();
        bool meldOutside = called.Any(m => m.Any(
            t => t.tile.Suit != suit && !t.tile.IsZ));
        int honitsuDistance = meldOutside
            ? Unreachable
            : TileCount - inSuit - honors;
        // A half flush with no honors at all is really a full flush; require at
        // least one honor so the two routes stay distinct.
        routes.Add(new YakuRoute {
          Tag = YakuTag.Honitsu, Key = "honitsu", ClosedHan = 3, OpenHan = 2,
          Distance = honors == 0 ? honitsuDistance + 2 : honitsuDistance,
        });
        bool meldOutsideStrict = called.Any(m => m.Any(t => t.tile.Suit != suit));
        routes.Add(new YakuRoute {
          Tag = YakuTag.Chinitsu, Key = "chinitsu", ClosedHan = 6, OpenHan = 5,
          Distance = meldOutsideStrict ? Unreachable : TileCount - inSuit,
        });
      }

      // ---- Shape-based -----------------------------------------------------
      routes.Add(new YakuRoute {
        Tag = YakuTag.Toitoi, Key = "toitoi", ClosedHan = 2, OpenHan = 2,
        Distance = hasOpenRun ? Unreachable : ToitoiDistance(),
      });
      routes.Add(new YakuRoute {
        Tag = YakuTag.Chanta, Key = "chanta", ClosedHan = 2, OpenHan = 1,
        Distance = ChantaDistance(includeHonors: true),
      });
      routes.Add(new YakuRoute {
        Tag = YakuTag.Junchan, Key = "junchan", ClosedHan = 3, OpenHan = 2,
        Distance = ChantaDistance(includeHonors: false),
      });
      routes.Add(new YakuRoute {
        Tag = YakuTag.Ittsu, Key = "ittsu", ClosedHan = 2, OpenHan = 1,
        Distance = meldHasYaochuu && !meldHasSimple ? IttsuDistance() + 2 : IttsuDistance(),
      });
      routes.Add(new YakuRoute {
        Tag = YakuTag.SanshokuDoujun, Key = "sanshoku", ClosedHan = 2, OpenHan = 1,
        Distance = SanshokuDistance(),
      });

      // Keep only the best (closest) route per tag+han so the greedy stacking
      // above never double counts two variants of the same yaku.
      return [.. routes
          .Where(r => r.Distance < Unreachable)
          .GroupBy(r => r.Key)
          .Select(g => g.OrderBy(r => r.Distance).First())];
    }

    private const int Unreachable = 999;

    /// <summary>
    /// Every honor that scores a yaku for this seat, keyed by WHY it scores.
    /// 5z/6z/7z are the dragons; the round wind and the seat wind each score
    /// separately, so an East triplet in an East round for the dealer yields two
    /// distinct entries.
    /// </summary>
    private static IEnumerable<(string Key, int Honor)> ValueHonorSources(
        PublicGameView view) {
      yield return ("yakuhai:haku", 5);
      yield return ("yakuhai:hatsu", 6);
      yield return ("yakuhai:chun", 7);
      yield return ("yakuhai:round", (int)view.RoundWind + 1);
      yield return ("yakuhai:seat", (int)view.SelfWind + 1);
    }

    private IEnumerable<int> ValueHonors(PublicGameView view) =>
        ValueHonorSources(view).Select(s => s.Honor);

    /// <summary>
    /// Pinfu wants four runs and a valueless pair, so every honor and every
    /// triplet already in the hand is a tile that has to go.
    /// </summary>
    private int PinfuDistance(PublicGameView view) {
      var (_, triplets, _) = Shape();
      int cost = triplets;
      foreach (var honor in ValueHonors(view).Distinct()) {
        cost += Count(TileSuit.Z, honor);
      }
      // Terminal-heavy hands make fewer two-sided waits, which pinfu requires.
      foreach (var suit in Suits) {
        cost += (Count(suit, 1) + Count(suit, 9)) / 2;
      }
      return cost;
    }

    private int ChiitoitsuDistance() {
      var (pairs, triplets, kinds) = Shape();
      // Triplets are dead weight for seven pairs: only one copy of each counts.
      int usablePairs = pairs + triplets;
      int shanten = 6 - usablePairs + System.Math.Max(0, 7 - kinds);
      return System.Math.Max(0, shanten - 2);
    }

    private int KokushiDistance() {
      int kinds = 0;
      bool hasPair = false;
      foreach (var suit in Suits) {
        foreach (int n in new[] { 1, 9 }) {
          int c = Count(suit, n);
          if (c > 0) kinds++;
          if (c > 1) hasPair = true;
        }
      }
      for (int n = 1; n <= 7; n++) {
        int c = Count(TileSuit.Z, n);
        if (c > 0) kinds++;
        if (c > 1) hasPair = true;
      }
      return 13 - kinds + (hasPair ? 0 : 1);
    }

    /// <summary> All triplets needs five pair-or-better blocks. </summary>
    private int ToitoiDistance() {
      var (pairs, triplets, _) = Shape();
      int blocks = pairs + triplets;
      return System.Math.Max(0, 5 - blocks) * 2;
    }

    /// <summary>
    /// Every block of a chanta hand must touch a terminal or honor, so the 4-5-6
    /// tiles - which can never sit in such a block - are exactly the tiles that
    /// have to be replaced. Junchan additionally forbids honors.
    /// </summary>
    private int ChantaDistance(bool includeHonors) {
      int cost = 0;
      foreach (var suit in Suits) {
        cost += Count(suit, 4) + Count(suit, 5) + Count(suit, 6);
      }
      if (!includeHonors) {
        cost += HonorTotal();
      }
      return cost;
    }

    /// <summary> Missing tiles of 123/456/789 in whichever suit is closest. </summary>
    private int IttsuDistance() {
      int best = Unreachable;
      foreach (var suit in Suits) {
        int missing = 0;
        for (int n = 1; n <= 9; n++) {
          if (Count(suit, n) == 0) missing++;
        }
        best = System.Math.Min(best, missing);
      }
      return best;
    }

    /// <summary> Missing tiles of the closest same-numbered run in all three suits. </summary>
    private int SanshokuDistance() {
      int best = Unreachable;
      for (int start = 1; start <= 7; start++) {
        int missing = 0;
        foreach (var suit in Suits) {
          for (int n = start; n < start + 3; n++) {
            if (Count(suit, n) == 0) missing++;
          }
        }
        best = System.Math.Min(best, missing);
      }
      return best;
    }

    #endregion
  }
}
