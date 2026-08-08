using RabiRiichi.Core;
using RabiRiichi.Patterns;
using System.Collections.Generic;

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
      freeTiles.Add(incoming);
      for (int i = 0; i < freeTiles.Count; i++) {
        var tile = freeTiles[i];
        freeTiles.RemoveAt(i);
        var score = patternResolver.ResolveMaxScore(player.hand, tile, PatternMask.All);
        if (score != null && (maxScore == null || score > maxScore)) {
          maxTile = tile;
          maxScore = score;
        }
        freeTiles.Insert(i, tile);
      }
      freeTiles.Remove(incoming);
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
