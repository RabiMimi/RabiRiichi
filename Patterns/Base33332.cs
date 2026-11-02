using RabiRiichi.Core;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Base33332 : BasePattern {
    #region Resolve
    private class ResolutionContext(GameTileBucket tileBucket, List<List<MenLike>> output) {
      internal class Refrigerator(Base33332.ResolutionContext context) : IDisposable {
        private readonly ResolutionContext context = context;
        private readonly Stack<GameTile> removed = new();
        private int groupNum = 0;

        public GameTile Remove(Tile tile) {
          var bucket = context.tileBucket.GetBucket(tile);
          var index = bucket.Count - 1;
          var ret = bucket[index];
          bucket.RemoveAt(index);
          removed.Push(ret);
          return ret;
        }

        public MenLike Remove(params Tile[] indexes) {
          var gr = MenLike.From(indexes.Select(Remove));
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

      public readonly List<MenLike> current = [];
      public readonly GameTileBucket tileBucket = tileBucket;
      public readonly List<List<MenLike>> output = output;

      public Refrigerator Save() {
        return new(this);
      }

      public void AddCurrentToOutput() {
        output.Add([.. current]);
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
              return t == incoming ? toExchange : t;
            }))
        ).ToList();
        output.Add(newGroup);
      }
    }

    private void DFSPattern(Tile curTile, int janCnt, ResolutionContext context) {
      if (!curTile.IsValid || curTile.IsWildcard) {
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
      using var save = context.Save();
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
        if (bucket.Count < 3) {
          break;
        }
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
        } else if (group is not (Kan or Kou or Shun)) {
          return false;
        }
      }
      // DFS output
      output = [];
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
    /// <inheritdoc/>
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 8) =>
        HandShapeAnalyzer.RegularShanten(hand, incoming, out output, maxShanten);
    #endregion Shanten
  }
}
