using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {

    public class Base13_1 : BasePattern {
        private static readonly Tiles T19Z = Tiles.T19Z;

        public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenOrJantou>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check hand & groups valid
            var buckets = GetTileGroups(hand, incoming, true).GetAllBuckets();
            List<MenOrJantou> ret = new();
            bool has2 = false;
            foreach (var gr in buckets) {
                if (gr.Count > 2 || !gr[0].tile.Is19Z) {
                    return false;
                }
                if (gr.Count == 2) {
                    if (has2) {
                        return false;
                    }
                    has2 = true;
                }
                ret.Add(MenOrJantou.From(gr));
            }
            output = new List<List<MenOrJantou>>() { ret };
            return true;
        }

        public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) {
            if (hand.fuuro.Count > 1 ||
                hand.fuuro.Any(gr => gr is not Jantou || !gr.All(t => t.tile.Is19Z))) {
                return Reject(out output);
            }

            var buckets = GetTileGroups(hand, incoming, true);
            var existing = new Tiles();
            int multiCnt = 0;
            foreach (var tile in T19Z) {
                int cnt = buckets.GetBucket(tile).Count;
                if (cnt > 0) {
                    existing.Add(tile);
                    multiCnt += (cnt > 1).ToInt();
                }
            }
            int ret = T19Z.Count - existing.Count - Math.Min(1, multiCnt);
            if (ret > maxShanten) {
                return Reject(out output);
            }
            if (incoming == null) {
                // 13张，计算有效进张
                if (multiCnt > 0) {
                    output = new Tiles(T19Z);
                    foreach (var t in existing) {
                        output.Remove(t);
                    }
                } else {
                    output = new Tiles(T19Z);
                }
            } else {
                // 14张，计算切牌
                var tiles = GetHand(hand.freeTiles, incoming);
                output = new Tiles(tiles
                    .Where(t => !t.Is19Z || buckets.GetBucket(t).Count > (multiCnt > 1 ? 1 : 2))
                    .Distinct());
            }
            return ret;
        }
    }
}
