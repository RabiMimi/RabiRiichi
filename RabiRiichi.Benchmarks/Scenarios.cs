using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame.Listener;
using RabiRiichi.Patterns;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Benchmarks {
  internal static class Scenarios {
    private const int MaxCap = Game.HAND_SIZE;
    private static readonly int[] WildcardCounts = [0, 1, 2, 3, 4, 6, 8, 13];

    internal static List<Scenario> All(int seed, int cases) {
      var result = new List<Scenario>();
      result.AddRange(Natural(seed, cases));
      foreach (int wildcards in WildcardCounts) {
        result.AddRange(Wildcard($"wild{wildcards}", new BenchTable(8, seed: seed), wildcards, seed, cases));
      }
      // Optional yaku enlarge both the evaluated graph and the bound, and calls
      // shrink the target space; both shift the cost relative to the plain table.
      foreach (int wildcards in new[] { 2, 4 }) {
        result.AddRange(Wildcard($"wild{wildcards}-koyaku", new BenchTable(8, koyaku: true, seed: seed), wildcards, seed, cases));
        result.AddRange(Wildcard($"wild{wildcards}-open", new BenchTable(8, seed: seed), wildcards, seed, cases, calledMelds: 1));
      }
      foreach (int wildcards in new[] { 8, 10, 12, 13 })
        result.AddRange(Wildcard($"aotenjou-koyaku/wild{wildcards}",
            new BenchTable(14, koyaku: true, seed: seed, aotenjou: true), wildcards, seed, cases));
      result.AddRange(HardCases(seed));
      foreach (int wall in new[] { 0, 4, 8, 16 }) result.Add(SelfPlay(wall, seed));
      return result;
    }

    private static BenchWork Work(ScoreStorage score) => score?.resolution == null ? BenchWork.None
        : new(score.resolution.VisitedTargets, score.resolution.EvaluatedInterpretations,
            score.resolution.VisitedNodes, score.resolution.BoundCalculations);

    private static BenchWork DiscardWork(BenchTable table, List<GameTile> tiles) {
      PlayTileAction.ComputeDiscardCandidates(table.player, tiles, tiles[^1], out var shared);
      return shared == null ? BenchWork.None : new(shared.VisitedTargets, shared.EvaluatedInterpretations,
          shared.VisitedNodes, shared.BoundCalculations, shared.Hits);
    }

    /// <summary>Everything the engine works out for the drawing seat before it can act.</summary>
    private static BenchWork DrawInquiry(BenchTable table, List<GameTile> tiles) {
      table.hand.pendingTile = tiles[^1];
      var inquiry = new MultiPlayerInquiry(table.game);
      foreach (var resolver in DrawTileListener.GetDrawTileResolvers(table.game)) {
        resolver.Resolve(table.player, tiles[^1], inquiry);
      }
      return BenchWork.None;
    }

    private static IEnumerable<Scenario> Natural(int seed, int cases) {
      var fixtures = new Fixtures(seed);
      var table = new BenchTable(0, seed: seed);
      var dealt13 = Enumerable.Range(0, cases).Select(_ => fixtures.Dealt(13)).ToList();
      var dealt14 = Enumerable.Range(0, cases).Select(_ => fixtures.Dealt(14)).ToList();
      var wins = Enumerable.Range(0, cases).Select(_ => fixtures.Complete()).ToList();
      var turns = wins.ConvertAll(fixtures.WithRandomDraw);
      var called = Enumerable.Range(0, cases).Select(_ => fixtures.CalledTriplet()).ToList();
      var openDealt = Enumerable.Range(0, cases).Select(_ => fixtures.Dealt(10)).ToList();
      var redWins = wins.ConvertAll(fixtures.WithRedTile);
      var resolver = table.resolver;
      T At<T>(List<T> list, int i) => list[i % cases];

      BenchWork Shanten(List<GameTile> tiles, int cap, List<MenLike> melds = null) {
        resolver.ResolveShanten(table.Holding(tiles, melds), null, out _, cap);
        return BenchWork.None;
      }
      BenchWork Discard(List<GameTile> tiles) {
        table.Holding(tiles.Take(13));
        return DiscardWork(table, tiles);
      }

      yield return new("natural/shanten/cap0", i => Shanten(At(dealt13, i), 0));
      yield return new("natural/shanten/cap1", i => Shanten(At(dealt13, i), 1));
      yield return new("natural/shanten/cap13", i => Shanten(At(dealt13, i), MaxCap));
      yield return new("natural/shanten/uncapped", i => Shanten(At(dealt13, i), int.MaxValue));
      yield return new("natural/shanten/open-cap13", i => Shanten(At(openDealt, i), MaxCap, [At(called, i)]));
      yield return new("natural/discard/dealt", i => Discard(At(dealt14, i)));
      yield return new("natural/discard/near-win", i => Discard(At(turns, i)));
      yield return new("natural/discard/win", i => Discard(At(wins, i)));
      yield return new("natural/draw/near-win", i => {
        table.Holding(At(turns, i).Take(13));
        return DrawInquiry(table, At(turns, i));
      });
      // The one draw where riichi may discard any tile, since every discard stays tenpai.
      yield return new("natural/draw/win", i => {
        table.Holding(At(redWins, i).Take(13));
        return DrawInquiry(table, At(redWins, i));
      });
      // What SetFuritenListener does per discard: every seat's waits, with only
      // the discarder's hand having changed since the previous discard.
      yield return new("natural/tenpai/four-seats", i => {
        for (int seat = 0; seat < 4; seat++) {
          _ = table.Holding(At(dealt13, seat == 3 ? i : seat)).Tenpai;
        }
        return BenchWork.None;
      });
      yield return new("natural/previews", i => {
        var h = table.Holding(At(wins, i).Take(13));
        var waits = h.Tenpai;
        if (waits.Count > 0) PlayTileAction.ComputeTenpaiInfos(resolver, h, waits);
        return BenchWork.None;
      });
      yield return new("natural/score/tsumo", i => {
        var t = At(wins, i);
        resolver.ResolveBestValidScore(table.Holding(t.Take(13)), Fixtures.AsWinningTile(t[13], true), PatternMask.All, 1);
        return BenchWork.None;
      });
      yield return new("natural/score/miss", i => {
        var t = At(dealt14, i);
        resolver.ResolveBestValidScore(table.Holding(t.Take(13)), Fixtures.AsWinningTile(t[13], false), PatternMask.All, 1);
        return BenchWork.None;
      });
    }

    private static IEnumerable<Scenario> Wildcard(string prefix, BenchTable table, int wildcards, int seed, int cases,
        int calledMelds = 0) {
      var fixtures = new Fixtures(seed + wildcards);
      var resolver = table.resolver;
      int melds = 4 - calledMelds;
      var called = Enumerable.Range(0, cases).Select(_ =>
          Enumerable.Range(0, calledMelds).Select(_ => fixtures.CalledTriplet()).ToList()).ToList();
      int free = melds * 3 + 2;
      var wins = Enumerable.Range(0, cases)
          .Select(_ => fixtures.WithWildcards(fixtures.Complete(melds), Math.Min(wildcards, free))).ToList();
      var nearWin = wins.ConvertAll(fixtures.WithRandomDraw);
      var dealt = Enumerable.Range(0, cases)
          .Select(_ => fixtures.WithWildcards(fixtures.Dealt(free), Math.Min(wildcards, free))).ToList();
      T At<T>(List<T> list, int i) => list[i % cases];

      Hand Holding(List<GameTile> tiles, int i) => table.Holding(tiles.Take(free - 1), At(called, i));
      BenchWork Score(int i, bool tsumo) {
        var t = At(wins, i);
        return Work(resolver.ResolveBestValidScore(Holding(t, i), Fixtures.AsWinningTile(t[free - 1], tsumo), PatternMask.All, 1));
      }
      BenchWork Discard(List<GameTile> tiles, int i) {
        Holding(tiles, i);
        return DiscardWork(table, tiles);
      }

      yield return new($"{prefix}/shanten/cap0", i => {
        resolver.ResolveShanten(Holding(At(wins, i), i), null, out _, 0);
        return BenchWork.None;
      });
      yield return new($"{prefix}/score/tsumo", i => Score(i, true));
      yield return new($"{prefix}/score/ron", i => Score(i, false));
      // Every discard asks this of each other seat, and usually the answer is no.
      yield return new($"{prefix}/score/miss", i => {
        var t = At(dealt, i);
        return Work(resolver.ResolveBestValidScore(Holding(t, i), Fixtures.AsWinningTile(t[free - 1], false), PatternMask.All, 1));
      });
      yield return new($"{prefix}/previews", i => {
        var h = Holding(At(wins, i), i);
        var waits = h.Tenpai;
        if (waits.Count == 0) return BenchWork.None;
        var previews = resolver.ResolveScorePreviews(h, waits, PatternMask.All, PatternMask.Luck);
        return Work(previews.Values.Select(p => p.Ron ?? p.Tsumo).FirstOrDefault(s => s?.resolution != null));
      });
      yield return new($"{prefix}/discard/near-win", i => Discard(At(nearWin, i), i));
      yield return new($"{prefix}/discard/dealt", i => Discard(At(dealt, i), i));
      yield return new($"{prefix}/draw/near-win", i => {
        Holding(At(nearWin, i), i);
        return DrawInquiry(table, At(nearWin, i));
      });
    }

    // Fixed examples from the wildcard performance investigation. The last tile
    // of a discard fixture is the incoming tile; previews use a 13-tile holding.
    private static IEnumerable<Scenario> HardCases(int seed) {
      var table = new BenchTable(14, koyaku: true, seed: seed, aotenjou: true);
      foreach (var (name, notation) in new[] {
        ("1m-12wild", "1m" + new string('*', 12)),
        ("allwild", new string('*', 13)),
      }) {
        var tiles = new Tiles(notation).Select((tile, i) => new GameTile(tile, 1000 + i)).ToList();
        yield return new($"aotenjou-koyaku/hard/previews/{name}", _ => {
          var hand = table.Holding(tiles);
          var scores = table.resolver.ResolveScorePreviews(hand, hand.Tenpai, PatternMask.All, PatternMask.Luck);
          return Work(scores.Values.Select(p => p.Ron ?? p.Tsumo).FirstOrDefault(s => s?.resolution != null));
        });
      }
      foreach (var (name, notation) in new[] {
        ("1m-2p-12wild", "1m2p" + new string('*', 12)),
        ("1z-7z-12wild", "1z7z" + new string('*', 12)),
        ("1m-13wild", "1m" + new string('*', 13)),
        ("123m-11z-9wild", "123m11z" + new string('*', 9)),
      }) {
        var tiles = new Tiles(notation).Select((tile, i) => new GameTile(tile, 1000 + i)).ToList();
        yield return new($"aotenjou-koyaku/hard/discard/{name}", _ => {
          table.Holding(tiles.Take(13));
          return DiscardWork(table, tiles);
        });
      }
    }

    /// <summary>A whole east-only game between four rule-based bots, on one of several fixed walls.</summary>
    private static Scenario SelfPlay(int wildcardsInWall, int seed) => new($"selfplay/wall{wildcardsInWall}", i => {
      var config = new GameConfig { playerCount = 4, seed = (ulong)(seed + i) };
      if (wildcardsInWall > 0) config.initialTiles = Fixtures.WallWithWildcards(wildcardsInWall);
      var room = new Room(new Random(seed + i), config);
      for (int p = 0; p < config.playerCount; p++) room.AddPlayer(new RuleBasedAI(-101 - p, room, UserStatus.InRoom));
      using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
      if (!room.RunHeadlessArenaMatch("benchmark", null, null, timeout.Token).GetAwaiter().GetResult()) {
        throw new InvalidOperationException("The headless match refused to start");
      }
      return BenchWork.None;
    }, MinOps: 1, MaxOps: 20);
  }
}