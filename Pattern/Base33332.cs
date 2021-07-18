using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    using DpLoc = ValueTuple<byte, byte, byte, byte>;

    public class Base33332 : BasePattern {
        private int M;
        private static readonly Tile LastMPS = new Tile("9s");
        private List<GameTiles> current;
        private List<List<GameTiles>> output;
        private GameTiles[] tileGroups;

        #region Resolve
        private class DFSHelper : IDisposable {
            private Base33332 instance;
            private Stack<(int, GameTile)> removed = new Stack<(int, GameTile)>();
            private int groupNum = 0;

            public DFSHelper(Base33332 instance) {
                this.instance = instance;
            }

            public GameTile Remove(int index) {
                var group = instance.tileGroups[index];
                var cnt = group.Count - 1;
                var ret = group[cnt];
                group.RemoveAt(cnt);
                removed.Push((index, ret));
                return ret;
            }

            public GameTiles Remove(params int[] indexes) {
                var gr = new GameTiles(indexes.Select(index => Remove(index)));
                instance.current.Add(gr);
                groupNum++;
                return gr;
            }

            public void Dispose() {
                while (removed.Count > 0) {
                    var item = removed.Pop();
                    instance.tileGroups[item.Item1].Add(item.Item2);
                }
                for (int i = 0; i < groupNum; i++) {
                    instance.current.RemoveAt(instance.current.Count - 1);
                }
            }
        }

        private void PrintGroups() {
            for (int i = 0; i < tileGroups.Length; i++) {
                if (tileGroups[i].Count > 0) {
                    Console.WriteLine($"{i}, {tileGroups[i].Count}");
                }
            }
        }

        private void DFSPattern(int index, int janCnt) {
            if (index >= tileGroups.Length) {
                if (janCnt == 1) {
                    output.Add(current.ToList());
                }
                return;
            }
            var cur = tileGroups[index];
            if (cur.Count == 0) {
                DFSPattern(index + 1, janCnt);
                return;
            }
            if (cur.Count >= 2 && janCnt < 1) {
                // 雀头
                using var helper = new DFSHelper(this);
                helper.Remove(index, index);
                DFSPattern(index, janCnt + 1);
            }
            // 枚举刻子数量
            var helpers = new Stack<DFSHelper>();
            while (true) {
                // 检查顺子
                if (cur.Count == 0) {
                    DFSPattern(index + 1, janCnt);
                } else if (index + 2 <= LastMPS.Val) {
                    int shunCnt = cur.Count;
                    var nxt = tileGroups[index + 1];
                    var nxt2 = tileGroups[index + 2];
                    if (nxt.Count >= shunCnt && nxt2.Count >= shunCnt) {
                        using var shunHelper = new DFSHelper(this);
                        for (int i = 0; i < shunCnt; i++) {
                            shunHelper.Remove(index, index + 1, index + 2);
                        }
                        DFSPattern(index + 1, janCnt);
                    }
                }
                if (cur.Count < 3) break;
                // 移除刻子
                var helper = new DFSHelper(this);
                helpers.Push(helper);
                helper.Remove(index, index, index);
            }
            while (helpers.Count > 0) {
                helpers.Pop().Dispose();
            }
        }

        public override bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check groups valid
            int janCnt = 0;
            foreach (var group in hand.groups) {
                if (group.IsJan) {
                    if (++janCnt > 1) {
                        return false;
                    }
                } else if (!(group.IsKan || group.IsKou || group.IsShun)) {
                    return false;
                }
            }
            // DFS output
            output = new List<List<GameTiles>>();
            tileGroups = GetTileGroups(hand, incoming, false);
            current = hand.groups;
            this.output = output;
            DFSPattern(0, janCnt);
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

        private DpVal[][,,,] dp;
        private Dictionary<DpLoc, int>[] visitedDp;

        private int ShantenDfs(int i, DpLoc loc, Tiles output) {
            if (i < 0) {
                return 0;
            }
            if (visitedDp[i].TryGetValue(loc, out int dist)) {
                return dist;
            }
            if (dp[i] == null) {
                dist = ShantenDfs(i - 1, loc, output);
                visitedDp[i].Add(loc, dist);
                return dist;
            }
            var cur = dp[i][loc.Item1, loc.Item2, loc.Item3, loc.Item4];
            dist = cur.dist;
            visitedDp[i].Add(loc, dist);
            bool shouldAdd = false;
            foreach (var pre in cur) {
                var preDist = ShantenDfs(i - 1, pre, output);
                shouldAdd |= preDist < dist;
            }
            if (shouldAdd) {
                var tile = new Tile((byte)i);
                if (!output.Contains(tile)) {
                    output.Add(tile);
                }
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
            tileGroups = GetTileGroups(hand, incoming, false);
            M = Math.Min(9, maxShanten);
            // 是否有雀头
            int janCnt = hand.groups.Count(gr => gr.IsJan);
            if (janCnt > 1) {
                return Reject(out output);
            }
            // 动态规划，dp[i][j2,j1,k,l]表示：
            // 当前在编号为i的牌
            // i-1有j2张未匹配
            // i有j1张未匹配
            // k表示是否匹配了雀头
            // l表示牌数-4
            // dp值表示前驱
            dp = new DpVal[tileGroups.Length][,,,];
            // 初始化dp值
            var dpPre = CreateDpSubArray();
            dp[0] = dpPre;
            dpPre[0, 0, janCnt, M] = new DpVal(0) { (0, 0, 0, 0) };
            int lasti = 0;
            int Lj = dpPre.GetLength(0);
            int Lk = dpPre.GetLength(2);
            int Ll = dpPre.GetLength(3);

            // 计算dp
            for (int i = 1; i < tileGroups.Length; i++) {
                var tile = new Tile((byte)i);
                if (!tile.IsValid) {
                    // 不合法
                    continue;
                }
                dp[i] = CreateDpSubArray();
                int N = tileGroups[i].Count;
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
                                            AddToList(ref dp[i][newJ2, newJ1, newK, newL],
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

                dpPre = dp[i];
                lasti = i;
            }

            // 检查非法输入
            DpLoc destLoc = (0, 0, 1, (byte)(incoming == null ? M + 1 : M));
            var dest = dpPre[destLoc.Item1, destLoc.Item2, destLoc.Item3, destLoc.Item4];
            if (dest == null || dest.dist > M) {
                return Reject(out output);
            }

            // 反向寻路
            output = new Tiles();
            visitedDp = new Dictionary<DpLoc, int>[lasti + 1];
            for (int i = 0; i <= lasti; i++) {
                visitedDp[i] = new Dictionary<DpLoc, int>();
            }
            ShantenDfs(lasti, destLoc, output);
            output.Sort();
            return dest.dist - 1;
        }
        #endregion Shanten
    }
}
