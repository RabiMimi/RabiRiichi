using RabiRiichi.Core;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class PatternResolver {
        public readonly HashSet<BasePattern> basePatterns = new();
        public readonly HashSet<StdPattern> stdPatterns = new();

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

        private class ResolutionContext {
            public List<MenLike> group;
            public Hand hand;
            public GameTile incoming;
            public BasePattern basePattern;
            public readonly HashSet<StdPattern> stdSuccess = new();
            public readonly HashSet<StdPattern> stdFailure = new();
        }

        private bool ResolveStdPattern(ResolutionContext context, StdPattern pattern, ScoreStorage scores) {
            // 检查是否已经计算过
            if (context.stdSuccess.Contains(pattern)) {
                return true;
            }
            if (context.stdFailure.Contains(pattern)) {
                return false;
            }

            // 检查底和
            if (!pattern.basePatterns.Contains(context.basePattern)) {
                // 不满足底和
                context.stdFailure.Add(pattern);
                return false;
            }

            // 计算依赖
            foreach (var ancestor in pattern.afterPatterns) {
                ResolveStdPattern(context, ancestor, scores);
            }

            // 计算条件
            if (!pattern.predicate(context.stdSuccess)) {
                // 不满足条件
                context.stdFailure.Add(pattern);
                return false;
            }

            // 计算当前役种
            using var fridge = scores.Freeze(!stdPatterns.Contains(pattern));
            if (pattern.Resolve(context.group, context.hand, context.incoming, scores)) {
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
        public ScoreStorage ResolveMaxScore(Hand hand, GameTile incoming, PatternMask patternMask) {
            var context = new ResolutionContext {
                hand = hand,
                incoming = incoming
            };

            IEnumerable<ScoreStorage> CalculateScores(BasePattern pattern) {
                if (!pattern.Resolve(hand, incoming, out var groups)) {
                    yield break;
                }
                context.basePattern = pattern;
                foreach (var group in groups) {
                    context.group = group;
                    context.stdSuccess.Clear();
                    context.stdFailure.Clear();
                    var scores = new ScoreStorage();
                    foreach (var stdPattern in stdPatterns.Where(p => p.type.HasAnyFlag(patternMask))) {
                        ResolveStdPattern(context, stdPattern, scores);
                    }
                    scores.Calc(hand.game.config.scoringOption);
                    yield return scores;
                }
            }

            var scoreStores = basePatterns.SelectMany(CalculateScores);

            return scoreStores.Max();
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