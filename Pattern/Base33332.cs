using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {

    public class Base33332 : BasePattern {
        private static readonly Tile LastMPS = new Tile("9s");
        private List<GameTiles> current;
        private List<List<GameTiles>> output;
        private GameTiles[] tileGroups;

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
    }
}
