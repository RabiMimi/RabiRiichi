using RabiRiichi.Action;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以立直
    /// </summary>
    public class RiichiResolver : ResolverBase {
        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerInquiry output) {
            if (hand.game.wall.NumRemaining < hand.game.gameInfo.config.playerCount) {
                return false;
            }
            if (hand.riichi || !hand.menzen || !incoming.IsTsumo) {
                return false;
            }
            Tiles riichiTiles = new Tiles();
            foreach (var pattern in Patterns.BasePatterns) {
                int shanten = pattern.Shanten(hand, incoming, out var tiles, 0);
                if (shanten < 0) {
                    // 和
                    riichiTiles = BasePattern.GetHand(hand.freeTiles, incoming);
                    break;
                }
                if (shanten > 0) {
                    continue;
                }
                riichiTiles.AddRange(tiles);
            }
            if (riichiTiles.Count == 0) {
                return false;
            }
            riichiTiles.Sort();
            var handRiichiTiles = hand.freeTiles.Where(t => riichiTiles.Contains(t.tile.WithoutDora)).ToList();
            output.Add(new RiichiAction(hand.player, handRiichiTiles));
            return true;
        }
    }
}
