using RabiRiichi.Core;
using RabiRiichi.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 判定是否可以天和
  /// </summary>
  public class TenhouResolver(PatternResolver patternResolver) : ResolverBase {
    protected readonly PatternResolver patternResolver = patternResolver;

    protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
      yield return player;
    }

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      var hand = player.hand;
      var freeTiles = hand.freeTiles;
      ScoreStorage maxScore = null;
      GameTile maxTile = null;
      var allTiles = freeTiles.Append(incoming).ToList();
      var cache = new Dictionary<(Tile, Generated.Core.TileSource, DiscardInfo), ScoreStorage>();
      bool canShare = patternResolver.CanShareScoring;
      for (int i = 0; i < allTiles.Count; i++) {
        var tile = allTiles[i];
        var key = (tile.tile, tile.source, tile.discardInfo);
        if (!canShare || !cache.TryGetValue(key, out var score)) {
          var remaining = allTiles.ToList(); remaining.RemoveAt(i);
          score = patternResolver.ResolveBestValidScore(hand.WithFreeTiles(remaining), tile,
              PatternMask.All, player.game.config.minHan);
          if (canShare) cache[key] = score;
        }
        if (score != null && (maxScore == null || score > maxScore)) {
          maxTile = tile;
          maxScore = score;
        }
      }
      if (maxScore != null && maxScore.result.IsValid(player.game.config.minHan)) {
        if (maxTile != incoming) {
          freeTiles.Remove(maxTile);
          freeTiles.Add(incoming);
          hand.pendingTile = maxTile;
          incoming = maxTile;
        }
        output.Add(new TsumoAction(player.id, maxScore, incoming), true);
        return true;
      }
      return false;
    }
  }
}
