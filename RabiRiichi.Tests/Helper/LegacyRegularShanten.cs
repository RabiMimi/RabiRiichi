using RabiRiichi.Core;
using RabiRiichi.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using DpLoc = System.ValueTuple<byte, byte, byte, byte>;

namespace RabiRiichi.Tests.Helper {
  /// <summary>
  /// The DP that used to back natural-hand shanten. It reaches its answers a
  /// different way from HandShapeAnalyzer, so it stays here as a test oracle.
  /// </summary>
  internal static class LegacyRegularShanten {
    private class DpVal(int dist = int.MaxValue) : List<DpLoc> {
      public int dist = dist;

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

    private static int ShantenDfs(Tile tile, DpLoc loc, Tiles output, DpContext ctx) {
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

    public static int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 8) {
      int maxDist = Math.Min(Game.HAND_SIZE, maxShanten) + 1; // 最大修改距离为向听数+1
      if (maxDist < 0) {
        { output = null; return int.MaxValue; }
      }
      if (maxDist == 0 && incoming == null) {
        { output = null; return int.MaxValue; }
      }
      var tileBucket = BasePattern.GetTileGroups(hand, incoming, false);
      maxDist = Math.Min(9, maxDist);

      DpVal[,,,] NewDpSubArray() {
        return new DpVal[3, 3, 2, (maxDist * 2) + 1];
      }

      // 是否有雀头
      int janCnt = hand.called.Count(gr => gr is Jantou);
      if (janCnt > 1) {
        { output = null; return int.MaxValue; }
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
      bool computeDiscard = BasePattern.ShouldComputeDiscard(hand, incoming);

      // 计算dp
      for (TileSuit i1 = TileSuit.M; i1 <= TileSuit.Z; i1++) {
        for (int i2 = 1; i2 <= (i1 == TileSuit.Z ? 7 : 9); i2++) {
          var tile = new Tile(i1, i2);
          var dpCur = NewDpSubArray();
          dp[(int)i1, i2] = dpCur;
          visitedDp[(int)i1, i2] = [];
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
                    int minCnt = l + ((newK - k) * 2) + j1 + j2 - N;
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
        { output = null; return int.MaxValue; }
      }

      // 反向寻路
      output = [];
      ShantenDfs(new Tile("7z"), destLoc, output, ctx);
      output.Sort();
      return dest.dist - 1;
    }
  }
}