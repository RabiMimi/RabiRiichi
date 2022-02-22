using System;
using System.Linq;
using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class PatternResolver {
        public readonly List<BasePattern> basePatterns = new();
        public readonly List<StdPattern> stdPatterns = new();
        public readonly List<BonusPattern> bonusPatterns = new();

        public void RegisterBasePattern(BasePattern pattern) {
            basePatterns.Add(pattern);
        }

        public void RegisterStdPattern(StdPattern pattern) {
            stdPatterns.Add(pattern);
        }

        public void RegisterBonusPattern(BonusPattern pattern) {
            bonusPatterns.Add(pattern);
        }

        private class ResolutionContext {
            public List<MenOrJantou> group;
            public Hand hand;
            public GameTile incoming;
            public Scorings scorings;
            public readonly HashSet<Type> baseSuccess = new();
            public readonly HashSet<Type> stdSuccess = new();
            public readonly HashSet<Type> stdFailure = new();
        }

        private bool ResolveStdPattern(ResolutionContext context, StdPattern pattern, Scorings scorings) {
            var pType = pattern.GetType();
            if (pattern.Resolve(context.group, context.hand, context.incoming, scorings)) {
                context.stdSuccess.Add(pType);
                return true;
            } else {
                context.stdFailure.Add(pType);
                return false;
            }
        }

        private bool ResolveStdPatternRecursive(ResolutionContext context, StdPattern pattern, Scorings scorings) {
            var pType = pattern.GetType();
            if (context.stdSuccess.Contains(pType))
                return true;
            if (context.stdFailure.Contains(pType))
                return false;
            // 检查底和
            if (!pattern.basePatterns.Any((basePattern) => context.baseSuccess.Contains(basePattern))) {
                // 不满足依赖
                context.stdFailure.Add(pType);
                return false;
            }
            // 检查依赖
            foreach (var dependency in pattern.dependOnPatterns) {
                var stdPattern = stdPatterns.Find(ptn => ptn.GetType().Equals(dependency));
                if (stdPattern != null) {
                    // 检查役种
                    if (!ResolveStdPatternRecursive(context, stdPattern, scorings)) {
                        // 不满足依赖
                        context.stdFailure.Add(pType);
                        return false;
                    }
                }
                // 没有找到依赖的pattern
                context.stdFailure.Add(pType);
                return false;
            }
            // 计算非必须的依赖
            foreach (var ancestor in pattern.afterPatterns) {
                var stdPattern = stdPatterns.Find(ptn => ptn.GetType().Equals(ancestor));
                if (stdPattern == null) {
                    // 没有找到依赖的pattern
                    // TODO: Log
                    // HUtil.Warn("未知的役种：" + ancestor.Name);
                    continue;
                }
                ResolveStdPatternRecursive(context, stdPattern, scorings);
            }
            return ResolveStdPattern(context, pattern, scorings);
        }

        /// <summary>
        /// 检查是否和牌并计算得分最高的牌型
        /// </summary>
        public Scorings ResolveMaxScore(Hand hand, GameTile incoming, bool applyBonus) {
            var groupList = new List<List<MenOrJantou>>();
            var context = new ResolutionContext {
                hand = hand,
                incoming = incoming,
                scorings = new Scorings()
            };

            foreach (var pattern in basePatterns) {
                if (pattern.Resolve(hand, incoming, out var groups)) {
                    context.baseSuccess.Add(pattern.GetType());
                    groupList.AddRange(groups);
                }
            }

            Scorings maxScore = null;
            foreach (var group in groupList) {
                context.stdSuccess.Clear();
                context.stdFailure.Clear();
                context.group = group;
                var scorings = new Scorings();
                foreach (var pattern in stdPatterns) {
                    ResolveStdPatternRecursive(context, pattern, scorings);
                }
                if (applyBonus) {
                    foreach (var pattern in bonusPatterns) {
                        ResolveStdPatternRecursive(context, pattern, scorings);
                    }
                }
                if (maxScore == null || maxScore < scorings) {
                    maxScore = scorings;
                }
            }
            return maxScore;
        }

        /// <summary>
        /// 获取手牌的向听数，并返回听牌或切牌
        /// <param name="hand">手牌</param>
        /// <param name="incoming">进张，若为null则计算听牌，否则计算切牌</param>
        /// <param name="maxShanten">不计算超过该向听数的结果以加速</param>
        /// </summary>
        public int ResolveShanten(Hand hand, GameTile incoming, out Tiles tiles, int maxShanten = int.MaxValue) {
            int retShanten = int.MaxValue;
            tiles = new Tiles();
            foreach (var pattern in Patterns.BasePatterns) {
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