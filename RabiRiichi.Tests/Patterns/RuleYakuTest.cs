using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class RuleYakuTest {
    // One runtime yaku per condition kind. Distinct values make each one move the optimum.
    private static RuleYaku[] RuntimeYaku(PatternResolver resolver) {
      var regular = resolver.basePatterns.OfType<Base33332>().Single();
      var pairs = resolver.basePatterns.OfType<Base72>().Single();
      var all = resolver.basePatterns.ToArray();
      T Std<T>() => resolver.stdPatterns.OfType<T>().Single();
      var winds = new RuleYaku("TwoWindTriplets", regular).When(Cond.Count(Group.Triplet.Of("1234z")) >= 2).Han(2);
      return [
        new RuleYaku("Kinmonkyou", all).When(Cond.AllTilesIn("2345678p") & Cond.Closed).Yakuman()
            .Replaces(Std<Honitsu>(), Std<Chinitsu>(), Std<Tanyao>()),
        winds,
        new RuleYaku("WindTsumo", regular).DependOn(winds).When(Cond.Tsumo).Han(1),
        new RuleYaku("FewSequences", regular).When(!Cond.AnyTileIn("1234567z") & (Cond.Count(Group.Sequence) <= 1)).Han(3),
        new RuleYaku("Fives", all).OfType(PatternMask.Bonus).When(Cond.TileCount("5m5p5s") >= 3).BonusHan(2),
        new RuleYaku("ClosedKanchan", regular).When(Cond.Wait(WaitShape.Kanchan) & Cond.Closed).Han(1),
        new RuleYaku("OutsideDragonPair", regular)
            .When(Cond.Every(Group.Any.Containing("19m19p19s1234567z")) & (Cond.Count(Group.Pair.Of("567z")) >= 1)).Han(2, 1),
        new RuleYaku("TwoConcealedTriplets", regular).When(Cond.Count(Group.Triplet.Closed()) >= 2).Han(1),
        new RuleYaku("WinOnFive", all).When(Cond.WinningTile("5m5p5s")).Han(1),
        new RuleYaku("SuitOrDragons", regular).When(Cond.OneSuit(true) | (Cond.Count(Group.Triplet.Of("567z")) >= 2)).Han(2),
        new RuleYaku("HonorPairs", pairs).When(Cond.AllTilesIn("19m19p19s1234567z") & (Cond.Count(Group.Pair.Of("1234567z")) >= 4)).Han(4),
        new RuleYaku("OpenHonorTriplet", regular).When(Cond.Count(Group.Triplet.Of("1234567z").Open()) >= 1).Han(1),
        new RuleYaku("FewTerminals", all).When(Cond.No(Group.Pair.Of("5m5p5s")) & (Cond.TileCount("19m19p19s") <= 2)).Han(1),
        // No partial tests: always correct, never prunes.
        new RuleYaku("Opaque", regular).When(Cond.Custom(w => w.TileCount(31) + w.TileCount(32) >= 3)).Han(1),
      ];
    }

    // Tiles at `wild` become wildcards; the tile at `win` is the winning tile.
    private static GameTile Split(Hand hand, string full, string wild, int win, bool tsumo) {
      var tiles = new Tiles(full);
      Assert.AreEqual(14, tiles.Count, full);
      foreach (int i in wild.Split(',').Select(int.Parse)) tiles[i] = Tile.Wildcard;
      var incoming = WildcardFixture.Incoming(tiles[win].ToString(), tsumo);
      tiles.RemoveAt(win);
      hand.freeTiles = tiles.ToGameTileList();
      return incoming;
    }

    // Every natural tile for every wildcard, scored by the legacy resolver.
    private static ScoreStorage Oracle(PatternResolver resolver, Hand hand, GameTile incoming, PatternMask mask) {
      var free = hand.freeTiles.Select(t => new GameTile(t.tile, t.traceId)).ToList();
      var clone = new GameTile(incoming.tile, incoming.traceId) { source = incoming.source, discardInfo = incoming.discardInfo };
      var copy = hand.WithFreeTiles(free);
      var wild = free.Append(clone).Where(t => t.tile.IsWildcard).ToArray();
      ScoreStorage best = null;
      void Assign(int i) {
        if (i == wild.Length) {
          var score = resolver.ResolveMaxScoreLegacy(copy, clone, mask);
          if (score != null && (best == null || score.CompareTo(best) > 0)) best = score;
          return;
        }
        foreach (var tile in Tiles.AllDistinct) { wild[i].tile = tile; Assign(i + 1); }
      }
      Assign(0);
      return best;
    }

    private static string Describe(ScoreStorage s) => s == null ? "null"
        : $"{s.result.han}h/{s.result.fu}f/{s.result.yakuman}y [{string.Join(" ", s.Select(x => $"{x.Src}:{x.Type}:{x.Val}"))}]";

    [DataTestMethod]
    [DataRow("234p456p678p33p888p", "4", 13, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("234p456p678p33p888p", "4,13", 13, true, ScoringOption.Default, PatternMask.All)]
    [DataRow("111z222z345m678s88s", "0,4", 13, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("111z222z345m678s88s", "0,4", 13, true, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("123m789p999s111z55z", "12", 6, true, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("123m456p123s567s88s", "0", 7, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("555m555p55s123s789s", "3,8", 0, true, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("555m555p55s123s789s", "3,8", 0, true, ScoringOption.Aotenjou, PatternMask.Bonus)]
    [DataRow("111m222m345p678s99s", "6", 2, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("123m456m789m11z555z", "9,13", 13, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("11m99m11p11z22z33z44z", "2,9", 13, false, ScoringOption.Aotenjou, PatternMask.All)]
    [DataRow("11m99m11p11z22z33z44z", "2,9", 13, false, ScoringOption.Yakuman | ScoringOption.KazoeYakuman, PatternMask.All)]
    public void RuntimeYakuMatchSubstitutionOracle(string full, string wild, int win, bool tsumo,
        ScoringOption option, PatternMask mask) {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = option;
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterStdPatterns(RuntimeYaku(resolver));
      var hand = game.players[0].hand;
      var incoming = Split(hand, full, wild, win, tsumo);
      var expected = Oracle(resolver, hand, incoming, mask);
      var actual = resolver.ResolveMaxScore(hand, incoming, mask);
      Assert.IsNotNull(expected, full);
      Assert.AreEqual(0, expected.CompareTo(actual), $"{full} wild {wild}: oracle {Describe(expected)}, search {Describe(actual)}");
    }

    [TestMethod]
    public void RuntimeYakuWithOpenMeldMatchSubstitutionOracle() {
      var game = WildcardFixture.Game(true);
      game.config.scoringOption = ScoringOption.Aotenjou;
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterStdPatterns(RuntimeYaku(resolver));
      var hand = WildcardFixture.Hand(game, "123m456p78s9s*");
      var pon = new Tiles("555z").ToGameTileList();
      pon[0].discardInfo = new(game.players[1], DiscardReason.Draw, 0);
      hand.called = [new Kou(pon)];
      hand.menzen = false;
      foreach (bool tsumo in new[] { false, true }) {
        var incoming = WildcardFixture.Incoming("9s", tsumo);
        var expected = Oracle(resolver, hand, incoming, PatternMask.All);
        var actual = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
        Assert.IsNotNull(expected);
        Assert.AreEqual(0, expected.CompareTo(actual), $"tsumo={tsumo}: oracle {Describe(expected)}, search {Describe(actual)}");
        Assert.IsTrue(actual.Any(s => s.Src == "OpenHonorTriplet"));
      }
    }

    [TestMethod]
    [Timeout(20_000)]
    public void RuntimeYakuOnNaturalHandsMatchLegacyEntries() {
      var game = WildcardFixture.Game(true);
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterStdPatterns(RuntimeYaku(resolver));
      var hand = game.players[0].hand;
      var random = new Random(4242);
      int[][] pools = [
        [.. Enumerable.Range(0, 34)], [.. Enumerable.Range(10, 7)],
        [0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33], [3, 4, 5, 13, 22, 27, 28, 31, 32, 33],
      ];
      int scored = 0;
      for (int sample = 0; sample < 120; sample++) {
        var pool = pools[sample % pools.Length];
        var full = sample % 6 == 5 ? RandomPairs(random, pool) : RandomWinning(random, pool);
        hand.called = [];
        int at = random.Next(full.Count);
        var physical = full[at]; full.RemoveAt(at);
        hand.freeTiles = full.ToGameTileList();
        var incoming = WildcardFixture.Incoming(physical.ToString(), sample % 2 == 0);
        foreach (var option in new[] { ScoringOption.Default, ScoringOption.Aotenjou }) {
          game.config.scoringOption = option;
          foreach (var mask in new[] { PatternMask.All, PatternMask.Regular, PatternMask.Bonus }) {
            var expected = resolver.ResolveMaxScoreLegacy(hand, incoming, mask);
            WildcardFixture.Same(expected, resolver.ResolveMaxScore(hand, incoming, mask), $"{sample}: {full}+{physical}; {option}/{mask}");
            if (expected?.Any(s => s.Source is RuleYaku) == true) scored++;
          }
        }
      }
      Assert.IsTrue(scored > 50, $"only {scored} results scored a runtime yaku");
    }

    private static Tiles RandomWinning(Random random, int[] pool) {
      var tiles = new Tiles();
      var starts = pool.Where(i => i < 27 && i % 9 < 7 && pool.Contains(i + 1) && pool.Contains(i + 2)).ToArray();
      for (int m = 0; m < 4; m++) {
        if (starts.Length > 0 && random.Next(2) == 0) {
          int i = starts[random.Next(starts.Length)];
          tiles.AddRange([CompactMeld.TileAt(i), CompactMeld.TileAt(i + 1), CompactMeld.TileAt(i + 2)]);
        } else {
          var t = CompactMeld.TileAt(pool[random.Next(pool.Length)]);
          tiles.AddRange([t, t, t]);
        }
      }
      var pair = CompactMeld.TileAt(pool[random.Next(pool.Length)]);
      tiles.AddRange([pair, pair]);
      return tiles;
    }

    private static Tiles RandomPairs(Random random, int[] pool) {
      var tiles = new Tiles();
      foreach (int i in pool.OrderBy(_ => random.Next()).Take(7)) tiles.AddRange([CompactMeld.TileAt(i), CompactMeld.TileAt(i)]);
      return tiles;
    }

    [TestMethod]
    [DoNotParallelize]
    [Timeout(20_000)]
    public void RuntimeYakumanKeepsAllWildcardSearchPruned() {
      var game = WildcardFixture.Game();
      var resolver = game.Get<PatternResolver>();
      resolver.RegisterStdPatterns(new RuleYaku("Kinmonkyou", resolver.basePatterns.ToArray())
          .When(Cond.AllTilesIn("2345678p") & Cond.Closed).Yakuman(10));
      var hand = WildcardFixture.Hand(game, new string('*', 13));
      var clock = Stopwatch.StartNew();
      var score = resolver.ResolveBestValidScore(hand, WildcardFixture.Incoming("*"), PatternMask.All, 1);
      Assert.IsNotNull(score);
      Assert.IsTrue(score.result.yakuman >= 10, Describe(score));
      Assert.IsTrue(score.Any(s => s.Src == "Kinmonkyou"));
      var allowed = new Tiles("2345678p");
      Assert.IsTrue(score.resolution.Groups.SelectMany(g => g.Tiles).All(t => allowed.Contains(t.Effective)));
      Assert.IsTrue(score.resolution.VisitedTargets < 100000, $"visited {score.resolution.VisitedTargets} targets");
      Console.WriteLine($"all-wild runtime yakuman: {clock.Elapsed.TotalMilliseconds:F2} ms; targets={score.resolution.VisitedTargets}; {Describe(score)}");
    }

    [TestMethod]
    public void WildcardTablesAcceptRuleYakuButRejectOpaqueRuntimeAdditions() {
      var config = new GameConfig { actionCenter = new JsonStringActionCenter(null), setup = new KinmonSetup() };
      config.initialTiles = new Tiles(config.initialTiles) { Tile.Wildcard };
      var game = new Game(config);
      foreach (var p in game.players) { p.Reset(); p.hand.jun = 2; }
      game.wall.Reset();
      var resolver = game.Get<PatternResolver>();
      var hand = WildcardFixture.Hand(game, "234p456p678p3p88p*");
      var incoming = WildcardFixture.Incoming("8p", false);
      var score = resolver.ResolveMaxScore(hand, incoming, PatternMask.All);
      Assert.IsNotNull(score);
      Assert.AreEqual(1, score.Single(s => s.Source is Kinmonkyou).Val);
      Assert.IsFalse(score.Any(s => s.Source is Tanyao or Chinitsu));
      // A RuleYaku added mid-game is scored too; plain StdPatterns cannot bound the search.
      resolver.RegisterStdPatterns(new RuleYaku("Late", resolver.basePatterns.ToArray()).When(Cond.WinningTile("8p")).Han(1));
      Assert.IsTrue(resolver.ResolveMaxScore(hand, incoming, PatternMask.All).Any(s => s.Src == "Late"));
      resolver.RegisterStdPatterns(new OpaqueRule(resolver.basePatterns.OfType<Base33332>().Single()));
      var error = Assert.ThrowsException<NotSupportedException>(() => resolver.ResolveMaxScore(hand, incoming, PatternMask.All));
      StringAssert.Contains(error.Message, nameof(OpaqueRule));
      StringAssert.Contains(error.Message, nameof(RuleYaku));
    }

    [TestMethod]
    public void RuleYakuIsFrozenOnceScoredAndRejectsNegativeValues() {
      var game = WildcardFixture.Game();
      var resolver = game.Get<PatternResolver>();
      var yaku = new RuleYaku("Frozen", resolver.basePatterns.ToArray()).Han(1);
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => yaku.Han(-1));
      resolver.RegisterStdPatterns(yaku);
      var score = resolver.ResolveMaxScore(WildcardFixture.Hand(game, "123m456p789s11z22z"), WildcardFixture.Incoming("2z"), PatternMask.All);
      Assert.AreEqual(1, score.Single(s => s.Source == yaku).Val);
      Assert.ThrowsException<InvalidOperationException>(() => yaku.Han(2));
    }

    // Every completion of a partial target must pass MayHold when it satisfies a
    // condition and MayFail when it violates it; otherwise the search would prune it.
    [TestMethod]
    [Timeout(20_000)]
    public void PartialTestsNeverRejectACompletion() {
      var game = WildcardFixture.Game();
      var hand = game.players[0].hand;
      var conditions = PropertyConditions();
      var random = new Random(7);
      var win = new WinView();
      var partial = new PartialView();
      long checks = 0;
      var clock = Stopwatch.StartNew();
      // ~0.5M checks; ~1.5s under coverage on Release.
      for (int sample = 0; sample < 60; sample++) {
        bool sevenPairs = sample % 4 == 3;
        var groups = new List<CompactMeld>();
        int fixedCount = 0;
        hand.menzen = true;
        if (sevenPairs) {
          foreach (int i in Enumerable.Range(0, 34).OrderBy(_ => random.Next()).Take(7)) groups.Add(new(CompactMeldKind.Pair, i, true));
        } else {
          if (sample % 3 == 0) {
            var kind = random.Next(3) switch { 0 => CompactMeldKind.Triplet, 1 => CompactMeldKind.Quad, _ => CompactMeldKind.Sequence };
            int i = kind == CompactMeldKind.Sequence ? random.Next(3) * 9 + random.Next(7) : random.Next(34);
            bool close = kind == CompactMeldKind.Quad && random.Next(2) == 0;
            groups.Add(new(kind, i, close, true));
            hand.menzen = close;
            fixedCount = 1;
          }
          groups.Add(new(CompactMeldKind.Pair, random.Next(34), true));
          while (groups.Count < 5) groups.Add(random.Next(2) == 0
              ? new(CompactMeldKind.Triplet, random.Next(34), true)
              : new(CompactMeldKind.Sequence, random.Next(3) * 9 + random.Next(7), true));
        }
        var all = groups.ToArray();
        int role = random.Next(fixedCount, all.Length);
        int index = all[role].TileIndex + (all[role].Kind == CompactMeldKind.Sequence ? random.Next(3) : 0);
        bool tsumo = random.Next(2) == 0;
        var incoming = new GameTile(CompactMeld.TileAt(index), -1) {
          source = tsumo ? TileSource.Wall : TileSource.Discard,
          discardInfo = tsumo ? null : new DiscardInfo(null, DiscardReason.Draw, 0),
        };
        var counts = PatternResolver.CountTargets(all);
        win.Set(hand, incoming, all, counts, role, index);
        // Physical naturals: a random subset of the free tiles other than the winning one.
        var natural = new int[34];
        var locked = PatternResolver.CountTargets(all.AsSpan(0, fixedCount));
        for (int i = 0; i < 34; i++) {
          int free = counts[i] - locked[i] - (i == index ? 1 : 0);
          for (int c = 0; c < free; c++) if (random.Next(2) == 0) natural[i]++;
        }
        bool naturalIncoming = random.Next(2) == 0;
        var required = (int[])natural.Clone();
        if (naturalIncoming) required[index]++;
        int melds = all.Count(g => g.Kind != CompactMeldKind.Pair);
        for (int k = sevenPairs ? 0 : fixedCount + 1; k <= all.Length; k++) {
          var placed = PatternResolver.CountTargets(all.AsSpan(0, k));
          ulong present = naturalIncoming ? 1UL << index : 0;
          for (int i = 0; i < 34; i++) if (placed[i] != 0 || required[i] != 0) present |= 1UL << i;
          partial.SetContext(hand, incoming);
          partial.Set(all, k, placed, locked, required, natural, naturalIncoming ? index : -1, present);
          if (sevenPairs) partial.SetRemaining(0, 7 - k, 0, (7 - k) * 2);
          else {
            int left = melds - all.Take(k).Count(g => g.Kind != CompactMeldKind.Pair);
            partial.SetRemaining(left, 0, 0, left * 3);
          }
          for (int c = 0; c < conditions.Length; c++) {
            var condition = conditions[c];
            bool holds = condition.Test(win);
            if (holds ? condition.MayHold(partial) : condition.MayFail(partial)) { checks++; continue; }
            Assert.Fail($"condition #{c} {(holds ? "holds" : "fails")} on [{string.Join(" ", all)}] "
                + $"(win {role}@{index}, tsumo={tsumo}) but its partial test rejects the first {k} groups");
          }
        }
      }
      Console.WriteLine($"{conditions.Length} conditions, {checks} admissibility checks in {clock.Elapsed.TotalMilliseconds:F0} ms");
    }

    private static YakuCondition[] PropertyConditions() {
      string[] sets = ["2345678p", "1234z", "567z", "19m19p19s1234567z", "123456789m", "5m5p5s", "1m2m"];
      GroupFilter[] filters = [Group.Any, Group.Meld, Group.Sequence, Group.Triplet, Group.Quad, Group.Pair];
      var basic = new List<YakuCondition> {
        Cond.OneSuit(true), Cond.OneSuit(false), Cond.Closed, Cond.Tsumo,
        Cond.Wait(WaitShape.Ryanmen), Cond.Wait(WaitShape.Tanki), Cond.WinningGroup(Group.Triplet),
      };
      foreach (var set in sets) {
        basic.AddRange([Cond.AllTilesIn(set), Cond.AnyTileIn(set), Cond.WinningTile(set),
            Cond.TileCount(set) >= 3, Cond.TileCount(set) <= 2]);
        foreach (var f in filters) {
          foreach (var g in new[] { f.Of(set), f.Containing(set), f.StartingAt(set).Closed(), f.Containing(set).Open() })
            basic.AddRange([Cond.Count(g) >= 2, Cond.Count(g) <= 0]);
          basic.Add(Cond.Every(f.Containing(set)));
        }
        basic.Add(Cond.Every(Group.Any.Closed()));
      }
      // Negation swaps the partial tests; conjunction and disjunction combine them.
      var conditions = new List<YakuCondition>(basic);
      for (int i = 0; i < basic.Count; i++) {
        conditions.Add(!basic[i]);
        conditions.Add(basic[i] & !basic[(i * 7 + 3) % basic.Count]);
        conditions.Add(basic[i] | basic[(i * 11 + 5) % basic.Count]);
      }
      return [.. conditions];
    }

    private sealed class KinmonSetup : RiichiSetup {
      protected override void InitPatterns() {
        base.InitPatterns();
        AddStdPattern<Kinmonkyou>();
      }
    }

    // Source-defined RuleYaku are constructed by dependency injection like built-in yaku.
    public sealed class Kinmonkyou : RuleYaku {
      public Kinmonkyou(AllBasePatterns families, Honitsu honitsu, Chinitsu chinitsu, Tanyao tanyao)
          : base(nameof(Kinmonkyou), families) {
        When(Cond.AllTilesIn("2345678p") & Cond.Closed).Yakuman().Replaces(honitsu, chinitsu, tanyao);
      }
    }

    private sealed class OpaqueRule : StdPattern {
      public OpaqueRule(Base33332 family) => BaseOn(family);
      public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) => false;
    }
  }
}
