using RabiRiichi.Action;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.ComponentModel;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以立直
    /// </summary>
    public class RiichiResolver : ResolverBase {
        private readonly PatternResolver patternResolver;
        public RiichiResolver(PatternResolver patternResolver) {
            this.patternResolver = patternResolver;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerInquiry output) {
            if (hand.game.wall.NumRemaining < hand.game.gameInfo.config.playerCount) {
                return false;
            }
            if (hand.riichi || !hand.menzen || incoming == null || !incoming.IsTsumo) {
                return false;
            }
            int shanten = patternResolver.ResolveShanten(hand, incoming, out var riichiTiles, 1);
            if (shanten > 1) {
                return false;
            }
            if (shanten == 0) {
                riichiTiles.AddRange(BasePattern.GetHand(hand.freeTiles, incoming).Distinct());
            }
            var handRiichiTiles = hand.freeTiles.Where(t => riichiTiles.Contains(t.tile.WithoutDora)).ToList();
            output.Add(new RiichiAction(hand.player, handRiichiTiles));
            return true;
        }
    }
}
