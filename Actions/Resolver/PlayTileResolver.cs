﻿using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Actions.Resolver {
    /// <summary>
    /// 生成切牌表
    /// </summary>
    public class PlayTileResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) => ResolveAction(player, incoming, output, null);

        public static bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output, Tiles forbidden) {
            var tiles = new List<GameTile>();
            var hand = player.hand;
            if (incoming != null) {
                tiles.Add(incoming);
            }
            if (!hand.riichi) {
                tiles.AddRange(hand.freeTiles);
            }
            if (forbidden != null) {
                tiles.RemoveAll(t => forbidden.Contains(t.tile.WithoutDora));
            }
            if (tiles.Count == 0) {
                return false;
            }
            tiles.Sort();
            output.Add(new PlayTileAction(player.id, tiles, incoming));
            return true;
        }
    }
}
