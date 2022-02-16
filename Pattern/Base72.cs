using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {

    public class Base72 : BasePattern {
        public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenOrJantou>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check groups
            if (hand.groups.Any(gr => !(gr is Jantou))) {
                return false;
            }
            // Check hand & groups valid
            var tileGroups = GetTileGroups(hand, incoming, true);
            var ret = new List<MenOrJantou>();
            foreach (var gr in tileGroups) {
                if (gr.Count == 0) {
                    continue;
                }
                if (gr.Count != 2) {
                    return false;
                }
                ret.Add(MenOrJantou.From(gr));
            }
            output = new List<List<MenOrJantou>> { ret };
            return true;
        }

        public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) {
            if (hand.groups.Any(gr => !(gr is Jantou))) {
                return Reject(out output);
            }

            var tileGroups = GetTileGroups(hand, incoming, true);
            var existingPairs = tileGroups.Where(tiles => tiles.Count >= 2).ToArray();
            var singleTiles = tileGroups
                .Where(tiles => tiles.Count == 1)
                .Select(tiles => tiles[0].tile.WithoutDora)
                .ToArray();
            int requiredSingle = 7 - existingPairs.Length;
            int ret = Game.HandSize
                - existingPairs.Length * 2
                - Math.Min(requiredSingle, singleTiles.Length);
            if (ret > maxShanten) {
                return Reject(out output);
            }
            if (incoming == null) {
                // 13张，计算进张
                if (singleTiles.Length >= requiredSingle) {
                    output = new Tiles(singleTiles);
                } else {
                    output = Tiles.AllDistinct;
                    foreach (var pair in existingPairs) {
                        output.Remove(pair[0].tile.WithoutDora);
                    }
                }
            } else {
                // 14张，计算切牌
                var tiles = GetHand(hand.freeTiles, incoming);
                output = new Tiles(tiles.Where(tile => {
                    int cnt = tileGroups[tile.NoDoraVal].Count;
                    if (singleTiles.Length > requiredSingle) {
                        return cnt > 2 || cnt == 1;
                    } else {
                        return cnt > 2;
                    }
                }).Distinct());
            }
            return ret;
        }
    }
}
