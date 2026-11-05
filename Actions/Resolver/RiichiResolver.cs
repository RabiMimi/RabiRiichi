using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 判定是否可以立直
  /// </summary>
  public class RiichiResolver(PatternResolver patternResolver) : ResolverBase {
    private readonly PatternResolver patternResolver = patternResolver;

    protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
      yield return player;
    }

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      var hand = player.hand;
      var config = player.game.config;
      if (config.riichiPolicy.HasAnyFlag(RiichiPolicy.SufficientTiles)
          && hand.game.wall.NumRemaining < config.playerCount) {
        return false;
      }
      if (config.MinRiichiPoints is { } minimumPoints && player.points < minimumPoints) {
        return false;
      }
      if (hand.riichi || !hand.menzen || incoming == null || !incoming.IsTsumo) {
        return false;
      }
      int shanten = patternResolver.ResolveShanten(hand, incoming, out var riichiTiles, 0);
      if (shanten >= 1) {
        return false;
      }
      if (shanten == -1) {
        riichiTiles.AddRange(BasePattern.GetHand(hand.freeTiles, incoming).Distinct());
      }
      var handRiichiTiles = hand.freeTiles.Append(incoming).Where(t => riichiTiles.Contains(t.tile.WithoutDora)).ToList();
      if (handRiichiTiles.Count > 0) {
        // The play action for this draw has already previewed these discards.
        var previews = output.GetByPlayerId(player.id)?.actions.OfType<PlayTileAction>().FirstOrDefault()?.CopyCandidates(handRiichiTiles);
        output.Add(previews == null ? new RiichiAction(player, handRiichiTiles, incoming)
            : new RiichiAction(player.id, handRiichiTiles, incoming) { candidates = previews });
        return true;
      }
      return false;
    }
  }
}