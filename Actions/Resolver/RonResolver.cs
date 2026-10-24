using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 判定是否可以和牌
  /// </summary>
  public class RonResolver(PatternResolver patternResolver) : ResolverBase {
    protected readonly PatternResolver patternResolver = patternResolver;

    protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
      return player.game.players.Where(p => !p.SamePlayer(player));
    }

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      var hand = player.hand;
      if (hand.isFuriten) {
        return false;
      }
      var maxScore = CalculateScore(player, incoming);
      if (maxScore != null && maxScore.result.IsValid(player.game.config.minHan)) {
        output.Add(new RonAction(hand.player.id, maxScore, incoming,
            player.game.config.agariOption.HasAnyFlag(AgariOption.FirstWinner)
                ? -incoming.discardInfo.fromPlayer.Dist(player) : 0), true);
        return true;
      }
      return false;
    }

    protected virtual ScoreStorage CalculateScore(Player player, GameTile incoming) =>
        patternResolver.ResolveBestValidScore(player.hand, incoming, PatternMask.All, player.game.config.minHan);
  }
}
