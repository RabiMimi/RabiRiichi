using RabiRiichi.Core;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using DpLoc = System.ValueTuple<byte, byte, byte, byte>;

namespace RabiRiichi.Patterns {
    public class Base33332 : BasePattern {
        #region Resolve
        private class ResolutionContext {
            internal class Refrigerator : IDisposable {
                private readonly ResolutionContext context;
                private readonly Stack<GameTile> removed = new();
                private int groupNum = 0;

                public Refrigerator(ResolutionContext context) {
                    this.context = context;
                }

                public GameTile Remove(Tile tile) {
                    var bucket = context.tileBucket.GetBucket(tile);
                    var index = bucket.Count - 1;
                    var ret = bucket[index];
                    bucket.RemoveAt(index);
                    removed.Push(ret);
                    return ret;
                }

                public MenLike Remove(params Tile[] indexes) {
                    var gr = MenLike.From(indexes.Select(index => Remove(index)));
                    context.current.Add(gr);
                    groupNum++;
                    return gr;
                }

                public void Dispose() {
                    while (removed.Count > 0) {
                        context.tileBucket.Add(removed.Pop());
                    }
                    context.current.RemoveRange(context.current.Count - groupNum, groupNum);
                }
            }

            public readonly List<MenLike> current = new();
            public readonly GameTileBucket tileBucket;
            public readonly List<List<MenLike>> output;

            public ResolutionContext(GameTileBucket tileBucket, List<List<MenLike>> output) {
                this.tileBucket = tileBucket;
                this.output = output;
            }

            public Refrigerator Save() => new(this);
            public void AddCurrentToOutput() => output.Add(current.ToList());
        }

        /// <summary> 和了牌算进哪一组会影响符数计算，因此需要生成不同组合 </summary>
        private static void GenerateOtherPatterns(List<MenLike> group, GameTile incoming, List<List<MenLike>> output) {
            // TODO:(Frenqy) 写test
            var existingMentsu = new List<ulong>();
            var incomingGroup = group.Find(gr => gr.Contains(incoming));
            Logger.Assert(incomingGroup != null, "无法在分组中找到和了牌位置");
            existingMentsu.Add(incomingGroup.Value);
            foreach (var gr in group) {
                if (existingMentsu.Contains(gr.Value)) {
                    continue;
                }
                var toExchange = gr.Find(t => t.tile.IsSame(incoming.tile));
                if (toExchange == null) {
                    continue;
                }
                existingMentsu.Add(gr.Value);
                var newGroup = group.Select(
                    g => MenLike.From(g.Select(t => {
                        if (t == toExchange) {
                            return incoming;
                        }
                        if (t == incoming) {
                            return toExchange;
                        }
                        return t;
                    }).ToList())
                ).ToList();
                output.Add(newGroup);
            }
        }

        private void DFSPattern(Tile curTile, int janCnt, ResolutionContext context) {
            if (!curTile.IsValid) {
                if (janCnt == 1) {
                    context.AddCurrentToOutput();
                }
                return;
            }

            // 计算下一张牌
            Tile nextTile = new(curTile.Suit, curTile.Num + 1);
            if (!nextTile.IsValid) {
                nextTile = new Tile(curTile.Suit + 1, 1);
            }

            // 获取当前牌桶
            var bucket = context.tileBucket.GetBucket(curTile);
            if (bucket.Count == 0) {
                DFSPattern(nextTile, janCnt, context);
                return;
            }
            if (bucket.Count >= 2 && janCnt < 1) {
                // 雀头
                using var savePoint = context.Save();
                savePoint.Remove(curTile, curTile);
                DFSPattern(curTile, janCnt + 1, context);
            }
            // 先存下来后面两张牌用于顺子计算
            var nxt1 = curTile.Next;
            var nxt2 = nxt1.Next;
            List<GameTile> bucket1 = null;
            List<GameTile> bucket2 = null;
            if (nxt2.IsValid) {
                bucket1 = context.tileBucket.GetBucket(nxt1.Suit, nxt1.Num);
                bucket2 = context.tileBucket.GetBucket(nxt2.Suit, nxt2.Num);
            }
            // 枚举刻子数量
            var save = context.Save();
            while (true) {
                // 检查顺子
                if (bucket.Count == 0) {
                    DFSPattern(nextTile, janCnt, context);
                } else if (nxt2.IsValid) {
                    int shunCnt = bucket.Count;
                    if (bucket1.Count >= shunCnt && bucket2.Count >= shunCnt) {
                        using var shunSavePoint = context.Save();
                        for (int i = 0; i < shunCnt; i++) {
                            shunSavePoint.Remove(curTile, nxt1, nxt2);
                        }
                        DFSPattern(nextTile, janCnt, context);
                    }
                }
                if (bucket.Count < 3)
                    break;
                // 移除刻子
                save.Remove(curTile, curTile, curTile);
            }
        }

        /// <inheritdoc/>
        public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HAND_SIZE + 1 : Game.HAND_SIZE)) {
                return false;
            }
            // Check groups valid
            int janCnt = 0;
            foreach (var group in hand.called) {
                if (group is Jantou) {
                    if (++janCnt > 1) {
                        return false;
                    }
                } else if (!(group is Kan || group is Kou || group is Shun)) {
                    return false;
                }
            }
            // DFS output
            output = new List<List<MenLike>>();
            var extraOutput = new List<List<MenLike>>();
            var context = new ResolutionContext(GetTileGroups(hand, incoming, false), output);
            DFSPattern(new Tile(TileSuit.M, 1), janCnt, context);
            foreach (var gr in output) {
                GenerateOtherPatterns(gr, incoming, extraOutput);
            }
            output.AddRange(extraOutput);
            foreach (var gr in output) {
                gr.InsertRange(0, hand.called);
            }
            return output.Count > 0;
        }
        #endregion Resolve

        #region Shanten
        private class DpVal : List<DpLoc> {
            public int dist;

            public DpVal(int dist = int.MaxValue) {
                this.dist = dist;
            }

            public void Update(int val, DpLoc loc) {
                if (val == dist) {
                    Add(loc);
                } else if (val < dist) {
                    dist = val;
                    Clear();
                    Add(loc);
                }
            }
        }

        private class DpContext {
            public readonly DpVal[,][,,,] dp = new DpVal[5, 10][,,,];
            public readonly Dictionary<DpLoc, int>[,] visitedDp = new Dictionary<DpLoc, int>[5, 10];

            public static void Add(ref DpVal list, int dist, DpLoc loc) {
                list ??= new DpVal();
                list.Update(dist, loc);
            }
        }

        private int ShantenDfs(Tile tile, DpLoc loc, Tiles output, DpContext ctx) {
            if (!tile.IsValid) {
                return 0;
            }
            int gr = (int)tile.Suit, num = tile.Num;
            Tile prev = new(tile.Suit, num - 1);
            if (!prev.IsValid) {
                prev = new Tile(tile.Suit - 1, 9);
            }
            if (ctx.visitedDp[gr, num].TryGetValue(loc, out int dist)) {
                return dist;
            }
            if (ctx.dp[gr, num] == null) {
                dist = ShantenDfs(prev, loc, output, ctx);
                ctx.visitedDp[gr, num].Add(loc, dist);
                return dist;
            }
            var cur = ctx.dp[gr, num][loc.Item1, loc.Item2, loc.Item3, loc.Item4];
            dist = cur.dist;
            ctx.visitedDp[gr, num].Add(loc, dist);
            bool shouldAdd = false;
            foreach (var pre in cur) {
                var preDist = ShantenDfs(prev, pre, output, ctx);
                shouldAdd |= preDist < dist;
            }
            if (shouldAdd && !output.Contains(tile)) {
                output.Add(tile);
            }
            return dist;
        }

        /// <inheritdoc/>
        public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 8) {
            int maxDist = Math.Min(Game.HAND_SIZE, maxShanten) + 1; // 最大修改距离为向听数+1
            if (maxDist < 0) {
                return Reject(out output);
            }
            if (maxDist == 0 && incoming == null) {
                return Reject(out output);
            }
            var tileBucket = GetTileGroups(hand, incoming, false);
            maxDist = Math.Min(9, maxDist);

            DpVal[,,,] NewDpSubArray() => new DpVal[3, 3, 2, maxDist * 2 + 1];

            // 是否有雀头
            int janCnt = hand.called.Count(gr => gr is Jantou);
            if (janCnt > 1) {
                return Reject(out output);
            }
            // 动态规划，dp[i1,i2][j2,j1,k,l]表示：
            // 当前在i1组编号为i2的牌
            // 上一张有j2张未匹配
            // 当前张有j1+j2张未匹配
            // k表示是否匹配了雀头
            // l表示牌数-4
            // dp值表示前驱
            var ctx = new DpContext();
            var dp = ctx.dp;
            var visitedDp = ctx.visitedDp;
            // 初始化dp值
            var dpPre = NewDpSubArray();
            dp[0, 9] = dpPre;
            dpPre[0, 0, janCnt, maxDist] = new DpVal(0) { (0, 0, 0, 0) };
            int Lj = dpPre.GetLength(0);
            int Lk = dpPre.GetLength(2);
            int Ll = dpPre.GetLength(3);
            bool computeDiscard = ShouldComputeDiscard(hand, incoming);

            // 计算dp
            for (TileSuit i1 = TileSuit.M; i1 <= TileSuit.Z; i1++) {
                for (int i2 = 1; i2 <= (i1 == TileSuit.Z ? 7 : 9); i2++) {
                    var tile = new Tile(i1, i2);
                    var dpCur = NewDpSubArray();
                    dp[(int)i1, i2] = dpCur;
                    visitedDp[(int)i1, i2] = new Dictionary<DpLoc, int>();
                    int N = tileBucket.GetBucket(tile).Count;
                    bool prevValid = tile.Prev.IsValid;
                    // 枚举上一个状态
                    for (int j2 = 0; j2 < Lj; j2++) {
                        for (int j1 = 0; j1 < Lj; j1++) {
                            for (int k = 0; k < Lk; k++) {
                                for (int l = 0; l < Ll; l++) {
                                    if (!prevValid) {
                                        // 无法连成顺子，禁止从上一个转移
                                        if (j2 > 0 || j1 > 0) {
                                            continue;
                                        }
                                    }
                                    var prevList = dpPre[j2, j1, k, l];
                                    if (prevList == null || prevList.Count == 0) {
                                        continue;
                                    }
                                    // i-2的所有顺子必须用完
                                    int newJ2 = j1;
                                    for (int newK = k; newK < Lk; newK++) {
                                        // 枚举新的手牌数
                                        int minCnt = l + (newK - k) * 2 + j1 + j2 - N;
                                        for (int newL = Math.Max(0, minCnt); newL < Ll; newL++) {
                                            int newJ1 = (newL - minCnt) % 3;
                                            DpContext.Add(ref dpCur[newJ2, newJ1, newK, newL],
                                                prevList.dist + Math.Max(0, computeDiscard ? (l - newL) : (newL - l)),
                                                ((byte)j2, (byte)j1, (byte)k, (byte)l));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    dpPre = dpCur;
                }
            }

            // 检查非法输入
            DpLoc destLoc = (0, 0, 1, (byte)(computeDiscard ? maxDist : maxDist + 1));
            var dest = dpPre[destLoc.Item1, destLoc.Item2, destLoc.Item3, destLoc.Item4];
            if (dest == null || dest.dist > maxDist) {
                return Reject(out output);
            }

            // 反向寻路
            output = new Tiles();
            ShantenDfs(new Tile("7z"), destLoc, output, ctx);
            output.Sort();
            return dest.dist - 1;
        }
        #endregion Shanten
    }
}
