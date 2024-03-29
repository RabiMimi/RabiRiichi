﻿using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
    /// <summary>
    /// 判定是否能碰
    /// </summary>
    public class PonResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile _) {
            return player.game.players.Where(p => !p.SamePlayer(player));
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            if (player.game.wall.IsHaitei) {
                return false;
            }
            var hand = player.hand;
            if (hand.riichi || incoming.IsTsumo) {
                return false;
            }
            var tile = incoming.tile.WithoutDora;
            var current = new List<GameTile> { incoming };
            var result = new List<List<GameTile>>();
            CheckCombo(hand.freeTiles, result, current, tile, tile);

            result.RemoveAll(tiles => !HasMoveAfterClaim(hand.freeTiles, player.game.config, tiles, incoming));

            if (result.Count == 0) {
                return false;
            }
            output.Add(new PonAction(player.id, result, -incoming.discardInfo.fromPlayer.Dist(player)));
            return true;
        }
    }
}
