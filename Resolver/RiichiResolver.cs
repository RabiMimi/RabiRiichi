using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以立直
    /// </summary>
    public class RiichiResolver : ResolverBase {
        public readonly List<BasePattern> basePatterns = new List<BasePattern>();

        private IEnumerable<string> GenerateRiichi(string suffix = "") {
            if (!string.IsNullOrEmpty(suffix) && suffix.StartsWith(" ")) {
                GenerateRiichi(" " + suffix);
            }
            yield return "rc" + suffix;
            yield return "riichi" + suffix;
            yield return "立直" + suffix;
            yield return "立" + suffix;
        }

        public void RegisterBasePattern(BasePattern pattern) {
            basePatterns.Add(pattern);
        }

        /// <summary>
        /// 获取听的牌
        /// </summary>
        /// <param name="hand">必须是13张</param>
        /// <returns>听牌列表，无赤宝牌</returns>
        public Tiles GetTenpai(Hand hand) {
            var ret = new Tiles();
            hand.hand.Sort();
            foreach (var pattern in basePatterns) {
                int shanten = pattern.Shanten(hand, null, out var tiles, 0);
                Debug.Assert(shanten >= 0);
                if (shanten > 0) {
                    continue;
                }
                ret.AddRange(tiles);
            }
            ret = new Tiles(ret.Distinct());
            ret.Sort();
            return ret;
        }

        /// <summary>
        /// 判定是否振听
        /// </summary>
        public bool IsFuriten(Hand hand) {
            var tenpai = GetTenpai(hand);
            // 摸切振听
            if (hand.discarded.Any(tile => tenpai.Contains(tile.tile.WithoutDora))) {
                return true;
            }
            if (hand.riichi) {
                // 立直振听
                return hand.game.AllDiscardedTiles
                    .Where(tile => tile.discardTime >= hand.riichiTile.discardTime)
                    .Any(tile => tenpai.Contains(tile.tile.WithoutDora));
            } else {
                // 同巡振听
                var discarded = hand.game.AllDiscardedTiles.ToList();
                int lastIndex = discarded.FindLastIndex(tile => tile.fromPlayer == hand.player);
                return discarded.Skip(lastIndex + 1).Any(tile => tenpai.Contains(tile.tile.WithoutDora));
            }
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.riichi || !hand.menzen) {
                return Reject(out output);
            }
            Tiles riichiTiles = new Tiles();
            var handTiles = BasePattern.GetHand(hand.hand, incoming);
            foreach (var pattern in basePatterns) {
                int shanten = pattern.Shanten(hand, incoming, out var tiles, 0);
                if (shanten < 0) {
                    // 和
                    riichiTiles = handTiles;
                    break;
                }
                if (shanten > 0) {
                    continue;
                }
                riichiTiles.AddRange(tiles);
            }
            if (riichiTiles.Count == 0) {
                return Reject(out output);
            }
            output = new PlayerActions();
            riichiTiles = new Tiles(handTiles.Intersect(riichiTiles).Distinct());
            hand.hand.Add(incoming);
            riichiTiles.Sort();
            foreach (var tile in riichiTiles) {
                var str = riichiTiles.Count <= 1 ? "" : tile.ToString();
                var gameTile = hand.hand.Find(t => t.tile == tile);
                hand.hand.Remove(gameTile);
                var tenpai = GetTenpai(hand);
                Debug.Assert(tenpai.Count > 0);
                hand.hand.Add(gameTile);
                output.Add(new PlayerAction {
                    options = GenerateRiichi(str).ToList(),
                    msg = new HMessage($"rc{str}：打{tile}立直，听{tenpai}"),
                    trigger = (_) => {
                        // TODO(Frenqy)
                    }
                });
            }
            hand.hand.Remove(incoming);
            return true;
        }
    }
}
