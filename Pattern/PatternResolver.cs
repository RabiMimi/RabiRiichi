using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class PatternResolver {
        public readonly HashSet<BasePattern> basePatterns = new();
        public readonly HashSet<StdPattern> stdPatterns = new();
        public readonly HashSet<StdPattern> bonusPatterns = new();

        public void RegisterBasePatterns(params BasePattern[] patterns) {
            foreach (var pattern in patterns) {
                basePatterns.Add(pattern);
            }
        }

        public void RegisterStdPatterns(params StdPattern[] patterns) {
            foreach (var pattern in patterns) {
                stdPatterns.Add(pattern);
            }
        }

        public void RegisterBonusPatterns(params StdPattern[] patterns) {
            foreach (var pattern in patterns) {
                bonusPatterns.Add(pattern);
            }
        }

        private class ResolutionContext {
            public List<MenLike> group;
            public Hand hand;
            public GameTile incoming;
            public readonly HashSet<BasePattern> baseSuccess = new();
            public readonly HashSet<StdPattern> stdSuccess = new();
            public readonly HashSet<StdPattern> stdFailure = new();
        }

        private bool ResolveStdPattern(ResolutionContext context, StdPattern pattern, Scorings scorings) {
            // 检查是否已经计算过
            if (context.stdSuccess.Contains(pattern)) {
                return true;
            }
            if (context.stdFailure.Contains(pattern)) {
                return false;
            }

            // 检查底和
            if (!pattern.basePatterns.Any((basePattern) => context.baseSuccess.Contains(basePattern))) {
                // 不满足底和
                context.stdFailure.Add(pattern);
                return false;
            }

            // 检查依赖
            foreach (var dependency in pattern.dependOnPatterns) {
                if (!ResolveStdPattern(context, dependency, scorings)) {
                    // 依赖失败
                    context.stdFailure.Add(pattern);
                    return false;
                }
            }

            // 计算非必须的依赖
            foreach (var ancestor in pattern.afterPatterns) {
                ResolveStdPattern(context, ancestor, scorings);
            }

            // 计算当前役种
            Scorings.Refrigerator fridge = null;
            if (!stdPatterns.Contains(pattern) && !bonusPatterns.Contains(pattern)) {
                fridge = scorings.Freeze();
            }
            bool isResolved = pattern.Resolve(context.group, context.hand, context.incoming, scorings);
            fridge?.Dispose();
            if (isResolved) {
                context.stdSuccess.Add(pattern);
                return true;
            } else {
                context.stdFailure.Add(pattern);
                return false;
            }
        }

        /// <summary>
        /// 检查是否和牌并计算得分最高的牌型
        /// </summary>
        public Scorings ResolveMaxScore(Hand hand, GameTile incoming, bool applyBonus) {
            var groupList = new List<List<MenLike>>();
            var context = new ResolutionContext {
                hand = hand,
                incoming = incoming
            };

            foreach (var pattern in basePatterns) {
                if (pattern.Resolve(hand, incoming, out var groups)) {
                    context.baseSuccess.Add(pattern);
                    groupList.AddRange(groups);
                }
            }

            var maxScore = groupList.Max(group => {
                context.stdSuccess.Clear();
                context.stdFailure.Clear();
                context.group = group;
                var scorings = new Scorings();
                foreach (var pattern in stdPatterns) {
                    ResolveStdPattern(context, pattern, scorings);
                }
                if (applyBonus) {
                    foreach (var pattern in bonusPatterns) {
                        ResolveStdPattern(context, pattern, scorings);
                    }
                }
                scorings.Calc();
                return scorings;
            });
            return maxScore;
        }

        /// <summary>
        /// 获取手牌的向听数，并返回听牌或切牌。
        /// 若向听数超过设定的限制，返回<see cref="int.MaxValue"/>。
        /// </summary>
        /// <param name="hand">手牌</param>
        /// <param name="incoming">进张，若为null则计算听牌，否则计算切牌</param>
        /// <param name="maxShanten">不计算超过该向听数的结果以加速</param>
        public int ResolveShanten(Hand hand, GameTile incoming, out Tiles tiles, int maxShanten = int.MaxValue) {
            int retShanten = int.MaxValue;
            tiles = new Tiles();
            foreach (var pattern in basePatterns) {
                int shanten = pattern.Shanten(hand, incoming, out var curTiles, maxShanten);
                if (shanten > retShanten || shanten == int.MaxValue) {
                    continue;
                }
                if (shanten == retShanten) {
                    tiles.AddRange(curTiles);
                    continue;
                }
                retShanten = shanten;
                tiles = curTiles;
            }
            tiles = new Tiles(tiles.Distinct().ToList());
            tiles.Sort();
            return retShanten;
        }
    }
}