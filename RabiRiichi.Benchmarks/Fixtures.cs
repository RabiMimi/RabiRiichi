using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Patterns.Compiled;

namespace RabiRiichi.Benchmarks {
  internal sealed class AllKoyakuSetup : RiichiSetup {
    protected override void InitPatterns() {
      base.InitPatterns();
      foreach (var pattern in KoyakuPatterns.ALL) AddStdPattern(pattern);
    }
  }

  /// <summary>A live game whose first seat is reused for every hypothetical hand.</summary>
  internal sealed class BenchTable {
    internal readonly Game game;
    internal readonly Player player;
    internal readonly Hand hand;
    internal readonly PatternResolver resolver;

    /// <param name="wildcardsInWall">Zero leaves the default wall and its natural-only code paths.</param>
    internal BenchTable(int wildcardsInWall, bool koyaku = false, int seed = 12345, bool aotenjou = false) {
      var config = new GameConfig {
        seed = unchecked((ulong)seed),
        scoringOption = aotenjou ? ScoringOption.Aotenjou : ScoringOption.Default,
        actionCenter = new JsonStringActionCenter(null),
        setup = koyaku ? new AllKoyakuSetup() : new RiichiSetup(),
      };
      if (wildcardsInWall > 0) config.initialTiles = Fixtures.WallWithWildcards(wildcardsInWall);
      game = new Game(config);
      foreach (var p in game.players) { p.Reset(); p.hand.jun = 2; }
      game.wall.Reset();
      player = game.players[0];
      hand = player.hand;
      resolver = game.Get<PatternResolver>();
    }

    internal Hand Holding(IEnumerable<GameTile> free, List<MenLike> called = null) {
      hand.freeTiles = [.. free];
      hand.called = called ?? [];
      hand.menzen = hand.called.All(g => g.IsClose);
      return hand;
    }
  }

  /// <summary>Deterministic hands: a given seed produces the same fixtures on any machine.</summary>
  internal sealed class Fixtures(int seed) {
    private readonly Random rng = new(seed);
    private int traceId = 1000;

    internal static Tiles WallWithWildcards(int count) =>
        new(Tiles.All.Value.Concat(Enumerable.Repeat(Tile.Wildcard, count)));

    private GameTile New(Tile tile) => new(tile, traceId++);

    internal static GameTile AsWinningTile(GameTile tile, bool tsumo) {
      tile.source = tsumo ? TileSource.Wall : TileSource.Discard;
      tile.discardInfo = tsumo ? null : new DiscardInfo(null, DiscardReason.Draw, 0);
      return tile;
    }

    /// <summary><paramref name="count"/> tiles dealt from a shuffled natural wall.</summary>
    internal List<GameTile> Dealt(int count) {
      var wall = Tiles.All.Value;
      return Enumerable.Range(0, wall.Count).OrderBy(_ => rng.Next()).Take(count).Select(i => New(wall[i])).ToList();
    }

    /// <summary>Random groups totalling <paramref name="melds"/> melds and a pair, shuffled.</summary>
    internal List<GameTile> Complete(int melds = 4) {
      while (true) {
        var counts = new int[34];
        var tiles = new List<int>();
        for (int m = 0; m < melds; m++) {
          int k = rng.Next(34);
          bool sequence = k < 27 && k % 9 <= 6 && rng.Next(3) > 0;
          tiles.AddRange(sequence ? [k, k + 1, k + 2] : [k, k, k]);
        }
        int pair = rng.Next(34);
        tiles.AddRange([pair, pair]);
        foreach (int k in tiles) counts[k]++;
        if (counts.Any(c => c > 4)) continue;
        return tiles.OrderBy(_ => rng.Next()).Select(k => New(CompactMeld.TileAt(k))).ToList();
      }
    }

    /// <summary>Replaces <paramref name="count"/> random tiles with wildcards.</summary>
    internal List<GameTile> WithWildcards(List<GameTile> tiles, int count) {
      var result = tiles.ToList();
      foreach (int i in Enumerable.Range(0, result.Count).OrderBy(_ => rng.Next()).Take(count)) {
        result[i] = New(Tile.Wildcard);
      }
      return result;
    }

    /// <summary>Swaps one tile for a random natural draw, leaving a hand close to winning.</summary>
    internal List<GameTile> WithRandomDraw(List<GameTile> tiles) {
      var result = tiles.ToList();
      result[rng.Next(result.Count)] = New(CompactMeld.TileAt(rng.Next(34)));
      return result;
    }

    /// <summary>Marks one random tile red. A custom wall may hold a red copy of any tile.</summary>
    internal List<GameTile> WithRedTile(List<GameTile> tiles) {
      var result = tiles.ToList();
      int i = rng.Next(result.Count);
      result[i] = New(new Tile(result[i].tile.Suit, result[i].tile.Num, true));
      return result;
    }

    /// <summary>A pon called from another seat.</summary>
    internal MenLike CalledTriplet() {
      var tile = CompactMeld.TileAt(rng.Next(34));
      var called = new[] { New(tile), New(tile), New(tile) };
      called[0].discardInfo = new DiscardInfo(null, DiscardReason.Draw, 0);
      called[0].source = TileSource.Discard;
      return MenLike.From(called);
    }
  }
}