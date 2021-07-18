using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 判定是否可以和牌
    /// </summary>
    public class RonResolver : ResolverBase {
        public readonly List<BasePattern> basePatterns = new List<BasePattern>();
        public readonly List<StdPattern> stdPatterns = new List<StdPattern>();
        public readonly List<StdPattern> bonusPatterns = new List<StdPattern>();
        /// <summary> 番缚 </summary>
        public int MinHan { get; private set; }

        private readonly HashSet<Type> baseSuccess = new HashSet<Type>();
        private readonly HashSet<Type> stdSuccess = new HashSet<Type>();
        private readonly HashSet<Type> stdFailure = new HashSet<Type>();
        private readonly List<List<GameTiles>> groupList = new List<List<GameTiles>>();
        private List<GameTiles> currentGroup;

        private Hand hand;
        private GameTile incoming;

        /// <summary> 设置番缚 </summary>
        public void SetMinHan(int val) {
            MinHan = val;
        }

        public void RegisterBasePattern(BasePattern pattern) {
            basePatterns.Add(pattern);
        }

        public void RegisterStdPattern(StdPattern pattern) {
            stdPatterns.Add(pattern);
        }

        public void RegisterBonusPattern(StdPattern pattern) {
            bonusPatterns.Add(pattern);
        }

        private bool ResolveStdPattern(StdPattern pattern, Scorings scorings) {
            var pType = pattern.GetType();
            if (pattern.Resolve(currentGroup, hand, incoming, scorings)) {
                stdSuccess.Add(pType);
                return true;
            } else {
                stdFailure.Add(pType);
                return false;
            }
        }

        private bool ResolveStdPatternRecursive(StdPattern pattern, Scorings scorings) {
            var pType = pattern.GetType();
            if (stdSuccess.Contains(pType))
                return true;
            if (stdFailure.Contains(pType))
                return false;
            foreach (var dependency in pattern.dependOnPatterns) {
                var basePattern = basePatterns.Find(ptn => ptn.GetType().Equals(dependency));
                if (basePattern != null) {
                    // 检查底和
                    if (!baseSuccess.Contains(dependency)) {
                        // 不满足依赖
                        stdFailure.Add(pType);
                        return false;
                    }
                    continue;
                }
                var stdPattern = stdPatterns.Find(ptn => ptn.GetType().Equals(dependency));
                if (stdPattern != null) {
                    // 检查役种
                    if (!ResolveStdPatternRecursive(stdPattern, scorings)) {
                        // 不满足依赖
                        stdFailure.Add(pType);
                        return false;
                    }
                }
                // 没有找到依赖的pattern
                stdFailure.Add(pType);
                return false;
            }
            // 计算依赖
            foreach (var ancestor in pattern.afterPatterns) {
                var stdPattern = stdPatterns.Find(ptn => ptn.GetType().Equals(ancestor));
                if (stdPattern == null) {
                    // 没有找到依赖的pattern
                    HUtil.Warn("未知的役种：" + ancestor.Name);
                    continue;
                }
                ResolveStdPatternRecursive(stdPattern, scorings);
            }
            return ResolveStdPattern(pattern, scorings);
        }

        private Scorings GetMaxScore(Hand hand, GameTile incoming, bool applyBonus) {
            this.hand = hand;
            this.incoming = incoming;
            groupList.Clear();
            baseSuccess.Clear();

            foreach (var pattern in basePatterns) {
                if (pattern.Resolve(hand, incoming, out var groups)) {
                    baseSuccess.Add(pattern.GetType());
                    groupList.AddRange(groups);
                }
            }

            Scorings maxScore = null;
            foreach (var group in groupList) {
                stdSuccess.Clear();
                stdFailure.Clear();
                currentGroup = group;
                var scorings = new Scorings();
                foreach (var pattern in stdPatterns) {
                    ResolveStdPatternRecursive(pattern, scorings);
                }
                if (applyBonus) {
                    foreach (var pattern in bonusPatterns) {
                        ResolveStdPatternRecursive(pattern, scorings);
                    }
                }
                if (maxScore == null || maxScore < scorings) {
                    maxScore = scorings;
                }
            }
            return maxScore;
        }

        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            if (hand.furiten && !incoming.IsTsumo) {
                return Reject(out output);
            }
            var maxScore = GetMaxScore(hand, incoming, false);
            if (maxScore.IsValid(MinHan)) {
                output = new PlayerActions {
                    new PlayerAction {
                        priority = PlayerAction.Priority.RON,
                        player = hand.player,
                        options = new List<string> {"ron", "r", "和", "hu", "he", "h"},
                        msg = new HMessage("r：和！"),
                        trigger = (_) => {
                            // TODO(Frenqy)
                            HUtil.Log("和了");
                        }
                    }
                };
                return true;
            } else {
                return Reject(out output);
            }
        }
    }
}
