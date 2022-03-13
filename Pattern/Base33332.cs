using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using DpLoc = System.ValueTuple<byte, byte, byte, byte>;

namespace RabiRiichi.Pattern {
    public class Base33332 : BasePattern {
        private int M;
        private List<MenLike> current;
        private List<List<MenLike>> output;
        private GameTileBucket tileBucket;

        #region Resolve
        private class DFSHelper : IDisposable {
            private readonly Base33332 instance;
            private readonly Stack<GameTile> removed = new();
            private int groupNum = 0;

            public DFSHelper(Base33332 instance) {
                this.instance = instance;
            }

            public GameTile Remove(Tile tile) {
                var bucket = instance.tileBucket.GetBucket(tile);
                var cnt = bucket.Count - 1;
                var ret = bucket[cnt];
                bucket.RemoveAt(cnt);
                removed.Push(ret);
                return ret;
            }

            public MenLike Remove(params Tile[] indexes) {
                var gr = MenLike.From(indexes.Select(index => Remove(index)).ToList());
                instance.current.Add(gr);
                groupNum++;
                return gr;
            }

            public void Dispose() {
                while (removed.Count > 0) {
                    var item = removed.Pop();
                    instance.tileBucket.Add(item);
                }
                for (int i = 0; i < groupNum; i++) {
                    instance.current.RemoveAt(instance.current.Count - 1);
                }
            }
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

        private void DFSPattern(Tile curTile, int janCnt) {
            if (!curTile.IsValid) {
                if (janCnt == 1) {
                    output.Add(current.ToList());
                }
                return;
            }

            // 计算下一张牌
            Tile nextTile = new(curTile.Suit, curTile.Num + 1);
            if (!nextTile.IsValid) {
                nextTile = new Tile(curTile.Suit + 1, 1);
            }

            // 获取当前牌桶
            var bucket = tileBucket.GetBucket(curTile);
            if (bucket.Count == 0) {
                DFSPattern(nextTile, janCnt);
                return;
            }
            if (bucket.Count >= 2 && janCnt < 1) {
                // 雀头
                using var helper = new DFSHelper(this);
                helper.Remove(curTile, curTile);
                DFSPattern(curTile, janCnt + 1);
            }
            // 先存下来后面两张牌用于顺子计算
            var nxt1 = curTile.Next;
            var nxt2 = nxt1.Next;
            GameTiles bucket1 = null;
            GameTiles bucket2 = null;
            if (nxt2.IsValid) {
                bucket1 = tileBucket.GetBucket(nxt1.Suit, nxt1.Num);
                bucket2 = tileBucket.GetBucket(nxt2.Suit, nxt2.Num);
            }
            // 枚举刻子数量
            var helpers = new Stack<DFSHelper>();
            while (true) {
                // 检查顺子
                if (bucket.Count == 0) {
                    DFSPattern(nextTile, janCnt);
                } else if (nxt2.IsValid) {
                    int shunCnt = bucket.Count;
                    if (bucket1.Count >= shunCnt && bucket2.Count >= shunCnt) {
                        using var shunHelper = new DFSHelper(this);
                        for (int i = 0; i < shunCnt; i++) {
                            shunHelper.Remove(curTile, nxt1, nxt2);
                        }
                        DFSPattern(nextTile, janCnt);
                    }
                }
                if (bucket.Count < 3)
                    break;
                // 移除刻子
                var helper = new DFSHelper(this);
                helpers.Push(helper);
                helper.Remove(curTile, curTile, curTile);
            }
            while (helpers.Count > 0) {
                helpers.Pop().Dispose();
            }
        }

        public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check groups valid
            int janCnt = 0;
            foreach (var group in hand.fuuro) {
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
            tileBucket = GetTileGroups(hand, incoming, false);
            current = hand.fuuro;
            this.output = output;
            DFSPattern(new Tile(TileSuit.M, 1), janCnt);
            foreach (var gr in output) {
                GenerateOtherPatterns(gr, incoming, extraOutput);
            }
            output.AddRange(extraOutput);
            return output.Count > 0;
        }
        #endregion Resolve

        #region Shanten
        class DpVal : List<DpLoc> {
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

        private DpVal[,,,] CreateDpSubArray() => new DpVal[5, 5, 2, M * 2 + 1];
        private static void AddToList(ref DpVal list, int dist, DpLoc loc) {
            if (list == null) {
                list = new DpVal();
            }
            list.Update(dist, loc);
        }

        private DpVal[,][,,,] dp;
        private Dictionary<DpLoc, int>[,] visitedDp;

        private int ShantenDfs(Tile tile, DpLoc loc, Tiles output) {
            if (!tile.IsValid) {
                return 0;
            }
            int gr = (int)tile.Suit, num = tile.Num;
            Tile prev = new(tile.Suit, num - 1);
            if (!prev.IsValid) {
                prev = new Tile(tile.Suit - 1, 9);
            }
            if (visitedDp[gr, num].TryGetValue(loc, out int dist)) {
                return dist;
            }
            if (dp[gr, num] == null) {
                dist = ShantenDfs(prev, loc, output);
                visitedDp[gr, num].Add(loc, dist);
                return dist;
            }
            var cur = dp[gr, num][loc.Item1, loc.Item2, loc.Item3, loc.Item4];
            dist = cur.dist;
            visitedDp[gr, num].Add(loc, dist);
            bool shouldAdd = false;
            foreach (var pre in cur) {
                var preDist = ShantenDfs(prev, pre, output);
                shouldAdd |= preDist < dist;
            }
            if (shouldAdd && !output.Contains(tile)) {
                output.Add(tile);
            }
            return dist;
        }

        public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 8) {
            if (maxShanten <= Game.HandSize) {
                maxShanten++;
            }
            if (maxShanten < 0) {
                return Reject(out output);
            }
            if (maxShanten == 0 && incoming == null) {
                return Reject(out output);
            }
            tileBucket = GetTileGroups(hand, incoming, false);
            M = Math.Min(9, maxShanten);
            // 是否有雀头
            int janCnt = hand.fuuro.Count(gr => gr is Jantou);
            if (janCnt > 1) {
                return Reject(out output);
            }
            // 动态规划，dp[i1,i2][j2,j1,k,l]表示：
            // 当前在i1组编号为i2的牌
            // 上一张有j2张未匹配
            // 当前张有j1张未匹配
            // k表示是否匹配了雀头
            // l表示牌数-4
            // dp值表示前驱
            dp = new DpVal[5, 10][,,,];
            visitedDp = new Dictionary<DpLoc, int>[5, 10];
            // 初始化dp值
            var dpPre = CreateDpSubArray();
            dp[0, 9] = dpPre;
            dpPre[0, 0, janCnt, M] = new DpVal(0) { (0, 0, 0, 0) };
            int Lj = dpPre.GetLength(0);
            int Lk = dpPre.GetLength(2);
            int Ll = dpPre.GetLength(3);

            // 计算dp
            for (TileSuit i1 = TileSuit.M; i1 <= TileSuit.Z; i1++) {
                for (int i2 = 1; i2 <= (i1 == TileSuit.Z ? 7 : 9); i2++) {
                    var tile = new Tile(i1, i2);
                    var dpCur = CreateDpSubArray();
                    dp[(int)i1, i2] = dpCur;
                    visitedDp[(int)i1, i2] = new Dictionary<DpLoc, int>();
                    int N = tileBucket.GetBucket(tile).Count;
                    bool prevValid = tile.Prev.IsValid;
                    // 枚举上一个状态
                    for (int j2 = 0; j2 < Lj; j2++) {
                        // 至少要和j2一样多，否则无法全部用完
                        for (int j1 = j2; j1 < Lj; j1++) {
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
                                    // 枚举减少Tile还是增加Tile
                                    for (int newJ1 = 0; newJ1 < Lj; newJ1++) {
                                        // i-2的所有顺子必须用完
                                        int newJ2 = j1 - j2;
                                        for (int newK = k; newK < Lk; newK++) {
                                            int deltaN = newJ1 + (newK - k) * 2 + j2 - N;
                                            if (deltaN < 0) {
                                                deltaN %= 3;
                                            }
                                            // deltaN < 0说明剩余牌比原先少，可能是打出去或刻子
                                            // deltaN > 0时一定是摸牌最优，不分情况
                                            for (int newL = l + deltaN + (deltaN < 0 ? 3 : 0);
                                                newL >= l + deltaN; newL -= 3) {
                                                if (newL < 0 || newL >= Ll) {
                                                    continue;
                                                }
                                                AddToList(ref dpCur[newJ2, newJ1, newK, newL],
                                                    prevList.dist + (incoming == null
                                                        ? Math.Max(0, newL - l)
                                                        : Math.Max(0, l - newL)),
                                                    ((byte)j2, (byte)j1, (byte)k, (byte)l));
                                            }
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
            DpLoc destLoc = (0, 0, 1, (byte)(incoming == null ? M + 1 : M));
            var dest = dpPre[destLoc.Item1, destLoc.Item2, destLoc.Item3, destLoc.Item4];
            if (dest == null || dest.dist > M) {
                return Reject(out output);
            }

            // 反向寻路
            output = new Tiles();
            ShantenDfs(new Tile("7z"), destLoc, output);
            output.Sort();
            return dest.dist - 1;
        }
        #endregion Shanten
    }
}
