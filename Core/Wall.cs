using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Core {
  public class Wall(RabiRand rand, GameConfig config) {
    public readonly GameConfig config = config;
    public readonly RabiRand rand = rand;

    private readonly RandIDPool idPool = new RandIDPool(rand);

    /// <summary> 宝牌数量 </summary>
    public const int NUM_DORA = 5;
    /// <summary> 基础岭上牌数量（不含拔北扩充） </summary>
    public const int NUM_RINSHAN = 4;
    /// <summary> 三人麻将的初始岭上牌数量。 </summary>
    public const int NUM_SANMA_RINSHAN = 8;
    /// <summary> 固定王牌数量。 </summary>
    public const int NUM_WANPAI = 14;
    /// <summary> 三人麻将初始宝牌/里宝牌指示牌对数。 </summary>
    public const int NUM_SANMA_INITIAL_DORA = 3;
    /// <summary>
    /// 初始岭上牌数量：四人麻将四张，三人麻将八张。
    /// </summary>
    public readonly int rinshanSize = config.playerCount == 3
        ? NUM_SANMA_RINSHAN
        : NUM_RINSHAN;
    public int initialDoraCount => config.playerCount == 3
        ? NUM_SANMA_INITIAL_DORA
        : NUM_DORA;
    /// <summary> 初始牌山全部136张牌的顺序 </summary>
    public List<GameTile> initialWall = [];
    /// <summary> 牌山剩下的牌 </summary>
    public readonly ListStack<GameTile> remaining = [];
    /// <summary> 岭上牌 </summary>
    public readonly ListStack<GameTile> rinshan = [];
    /// <summary> 宝牌 </summary>
    public readonly ListStack<GameTile> doras = [];
    /// <summary> 里宝牌 </summary>
    public readonly ListStack<GameTile> uradoras = [];
    /// <summary> 从海底端补入王牌、但不作为指示牌使用的牌。 </summary>
    public readonly ListStack<GameTile> wanpaiFillers = [];
    /// <summary> 已翻的Dora数量 </summary>
    public int revealedDoraCount = 0;
    /// <summary> 已翻的里Dora数量 </summary>
    public int revealedUradoraCount = 0;
    /// <summary> 玩家还尚不知道的牌，用于搞事 </summary>
    public IEnumerable<GameTile> hiddenTiles =>
        remaining
        .Concat(rinshan)
        .Concat(doras.Skip(revealedDoraCount))
        .Concat(uradoras)
        .Concat(wanpaiFillers);
    /// <summary> 牌山剩下的牌数 </summary>
    public int NumRemaining => remaining.Count;
    /// <summary> 是否到了海底 </summary>
    public bool IsHaitei => NumRemaining <= 0;

    /// <summary> 重置牌山 </summary>
    public void Reset() {
      remaining.Clear();
      rinshan.Clear();
      doras.Clear();
      uradoras.Clear();
      wanpaiFillers.Clear();
      idPool.Reset();
      revealedDoraCount = 0;
      revealedUradoraCount = 0;
      var physicalWall = new ListStack<GameTile>(
          config.initialTiles.Select(tile => new GameTile(tile, idPool.GetID())));
      rand.Shuffle(physicalWall);
      Logger.Assert(physicalWall.Count >= NUM_WANPAI,
          $"Wall must contain at least {NUM_WANPAI} tiles");

      // The shuffled list is the flattened physical wall after the break: live
      // wall in draw order, followed by the fixed 14-tile dead wall.
      initialWall = [.. physicalWall];
      int deadWallStart = physicalWall.Count - NUM_WANPAI;
      for (int i = deadWallStart - 1; i >= 0; i--) {
        remaining.Add(physicalWall[i]);
      }

      // Physical order: (DoraN,UraN) ... (Dora1,Ura1).
      for (int k = 1; k <= initialDoraCount; k++) {
        int pair = initialDoraCount - k;
        doras.Add(physicalWall[deadWallStart + pair * 2]);
        uradoras.Add(physicalWall[deadWallStart + pair * 2 + 1]);
      }

      // Then (... R3,R4,R1,R2). Store in reverse draw order so Pop returns
      // R1, R2, ... R8.
      int rinshanStart = deadWallStart + initialDoraCount * 2;
      int pairCount = rinshanSize / 2;
      for (int n = rinshanSize; n >= 1; n--) {
        int drawPair = (n - 1) / 2;
        int physicalPair = pairCount - 1 - drawPair;
        int physicalIndex = rinshanStart + physicalPair * 2 + (n - 1) % 2;
        rinshan.Add(physicalWall[physicalIndex]);
      }
    }

    /// <summary>
    /// 构建牌山的展示布局（客户端用于渲染/录像）。这是一个扁平化的物理牌墙表示，
    /// 顺序 = 摸牌顺序（先摸的在前；每墩内上面的牌先摸，因此上牌在前、下牌在后）。
    /// 王牌固定在末尾，布局如下（左到右，每墩上-下）：
    /// <code>
    /// 上排: ... Dora5 Dora4 Dora3 Dora2 Dora1 Rinshan3 Rinshan1
    /// 下排: ... Ura5  Ura4  Ura3  Ura2  Ura1  Rinshan4 Rinshan2
    /// </code>
    /// 其中 DoraK/UraK 为第K张（会）翻出的指示牌（Dora1最先翻），RinshanK 为第K张
    /// （会）摸出的岭上牌（Rinshan1最先摸）。岭上牌数量可变。
    /// </summary>
    public void RebuildInitialWall() {
      initialWall = BuildInitialWall();
    }

    private List<GameTile> BuildInitialWall() {
      var result = new List<GameTile>();
      // 牌河（可摸区）：摸牌从 remaining 末尾开始，因此摸牌顺序 = remaining 反序。
      for (int i = remaining.Count - 1; i >= 0; i--) {
        result.Add(remaining[i]);
      }
      // 王牌：Dora5..Dora1 与 Ura5..Ura1 交错成墩（上=Dora，下=Ura）。
      for (int k = doras.Count; k >= 1; k--) {
        result.Add(doras[k - 1]);
        result.Add(uradoras[k - 1]);
      }
      // 岭上：先摸的（Rinshan1）在牌墙最右墩的上面。DrawRinshan 从 rinshan 末尾摸，
      // 所以 Rinshan1 = rinshan[^1]。按墩排列：... (Rinshan3,Rinshan4)(Rinshan1,Rinshan2)。
      // 即成对分组，编号大的墩在左，(1,2) 墩在最右。
      int pairCount = (rinshan.Count + 1) / 2;
      for (int pair = pairCount - 1; pair >= 0; pair--) {
        int top = 2 * pair;      // Rinshan(2*pair+1) 的0基索引：越先摸下标越大
        int bottom = 2 * pair + 1;
        result.Add(rinshan[rinshan.Count - 1 - top]);
        if (bottom < rinshan.Count) {
          result.Add(rinshan[rinshan.Count - 1 - bottom]);
        }
      }
      return result;
    }

    /// <summary> 检查牌山是否还有给定的牌数 </summary>
    public bool Has(int amount) {
      return NumRemaining >= amount;
    }


    /// <summary> 抽一张牌 </summary>
    public GameTile Draw() {
      var ret = remaining.Pop();
      ret.source = TileSource.Wall;
      return ret;
    }

    /// <summary> 抽若干张牌 </summary>
    public List<GameTile> Draw(int count) {
      var ret = remaining.PopMany(count).ToList();
      foreach (var tile in ret) {
        tile.source = TileSource.Wall;
      }
      return ret;
    }

    /// <summary> 翻一张宝牌 </summary>
    public GameTile RevealDora(bool isKan) {
      bool revealUradora = config.doraOption.HasAnyFlag(
          isKan ? DoraOption.KanUradora : DoraOption.InitialUradora);
      bool revealDora = config.doraOption.HasAnyFlag(
          isKan ? DoraOption.KanDora : DoraOption.InitialDora);
      if (revealUradora && revealedUradoraCount < uradoras.Count) {
        revealedUradoraCount++;
      }
      if (revealDora && revealedDoraCount < doras.Count) {
        var ret = doras[revealedDoraCount++];
        ret.source = TileSource.Wanpai;
        return ret;
      }
      return null;
    }

    /// <summary> 计算tile算几番宝牌（不考虑里宝牌/红宝牌）。非宝牌返回0 </summary>
    public int CountDora(Tile tile) {
      return doras.Take(revealedDoraCount).Count(dora => dora.tile.NextDora.IsSame(tile));
    }

    /// <summary> 计算tile中几番里宝牌。非里宝牌返回0 </summary>
    public int CountUradora(Tile tile) {
      return uradoras.Take(revealedUradoraCount).Count(uradora => uradora.tile.NextDora.IsSame(tile));
    }

    /// <summary> 抽一张岭上牌 </summary>
    public GameTile DrawRinshan() {
      var ret = rinshan.Pop();
      ret.source = TileSource.Wanpai;
      ReplenishWanpai();
      return ret;
    }

    /// <summary>
    /// Move the live wall's haitei tile to the indicator side after every
    /// replacement draw, keeping the dead wall at 14 tiles. In sanma the first
    /// four moved tiles become Ura4, Dora4, Ura5, Dora5 in physical order.
    /// </summary>
    private void ReplenishWanpai() {
      Logger.Assert(remaining.Count > 0,
          "Cannot replenish wanpai after the live wall is exhausted");
      var tile = remaining[0];
      remaining.RemoveAt(0);

      // Pop has already removed this replacement tile, so the difference is the
      // 1-based replacement-draw number: 1 after the first draw, up to 8.
      int replacementDrawNumber = rinshanSize - rinshan.Count;
      int missingSanmaIndicatorTiles = (NUM_DORA - initialDoraCount) * 2;
      if (config.playerCount != 3
          || replacementDrawNumber > missingSanmaIndicatorTiles) {
        wanpaiFillers.Add(tile);
        return;
      }

      // Expanding the dead wall leftward encounters each physical stack from
      // bottom to top: Ura4, Dora4, then Ura5, Dora5.
      if (replacementDrawNumber % 2 == 1) {
        uradoras.Add(tile);
      } else {
        doras.Add(tile);
      }
    }

    /// <summary>
    /// 去掉一张玩家未知牌（需要在<see cref="hiddenTiles"/>中）
    /// 若该牌在王牌里，则牌山最后一张牌会补充王牌
    /// </summary>
    public bool Remove(GameTile tile) {
      if (remaining.Remove(tile)) {
        return true;
      }

      if (IsHaitei) {
        return false;
      }

      var newTile = remaining[0];
      int index;
      if ((index = rinshan.IndexOf(tile)) >= 0) {
        remaining.RemoveAt(0);
        rinshan[index] = newTile;
        return true;
      }
      if ((index = uradoras.IndexOf(tile)) >= 0) {
        remaining.RemoveAt(0);
        uradoras[index] = newTile;
        return true;
      }
      if ((index = doras.IndexOf(tile)) >= revealedDoraCount) {
        remaining.RemoveAt(0);
        doras[index] = newTile;
        return true;
      }
      if ((index = wanpaiFillers.IndexOf(tile)) >= 0) {
        remaining.RemoveAt(0);
        wanpaiFillers[index] = newTile;
        return true;
      }
      return false;
    }

    /// <summary>
    /// 若tile在searchFrom中，将tile与target的targetIndex位置的tile进行交换
    /// </summary>
    private static bool Swap(ListStack<GameTile> target, int targetIndex, ListStack<GameTile> searchFrom, GameTile tile) {
      int index = searchFrom.IndexOf(tile);
      if (index < 0) {
        return false;
      }

      (target[targetIndex], searchFrom[index]) = (searchFrom[index], target[targetIndex]);
      return true;
    }

    /// <summary>
    /// 将tile与target的targetIndex位置的tile进行交换
    /// </summary>
    private void Swap(ListStack<GameTile> target, int targetIndex, GameTile tile) {
      if (Swap(target, targetIndex, remaining, tile)) {
        return;
      }

      if (Swap(target, targetIndex, rinshan, tile)) {
        return;
      }

      if (Swap(target, targetIndex, uradoras, tile)) {
        return;
      }

      if (Swap(target, targetIndex, wanpaiFillers, tile)) {
        return;
      }

      int index = doras.IndexOf(tile);
      if (index >= revealedDoraCount) {
        (doras[index], target[targetIndex]) = (target[targetIndex], doras[index]);
        return;
      }
      throw new ArgumentException("tile is already drawn or revealed, cannot swap");
    }

    /// <summary>
    /// 用tile替换target的targetIndex位置的牌
    /// </summary>
    /// <returns>被替换的牌</returns>
    private static GameTile Replace(ListStack<GameTile> target, int targetIndex, GameTile tile) {
      var ret = target[targetIndex];
      target[targetIndex] = tile;
      return ret;
    }

    /// <summary>
    /// 在hiddenTiles中寻找tile对应的牌
    /// </summary>
    public GameTile FindInHidden(Tile tile, bool ignoreAkadora = true) {
      return hiddenTiles.FirstOrDefault(t => ignoreAkadora ? t.tile.IsSame(tile) : t.tile == tile);
    }

    /// <summary> 将一张牌作为牌山第i前的牌（从0开始） </summary>
    public void Insert(int i, GameTile tile) {
      Remove(tile);
      tile.traceId = idPool.GetID();
      remaining.Insert(remaining.Count - i, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换牌山里第i张牌（从0开始） </summary>
    /// <returns>被替换的牌</returns>
    public GameTile Replace(int i, GameTile tile) {
      tile.traceId = idPool.GetID();
      i = remaining.Count - i - 1;
      var ret = remaining[i];
      remaining[i] = tile;
      return ret;
    }

    /// <summary> 将一张牌放到牌山最前 </summary>
    public void InsertFirst(GameTile tile) {
      Insert(0, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换牌山最前的牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceFirst(GameTile tile) {
      return Replace(0, tile);
    }

    /// <summary> 将一张牌放到牌山最后 </summary>
    public void InsertLast(GameTile tile) {
      if (remaining.Contains(tile)) {
        Insert(remaining.Count - 1, tile);
      } else {
        Insert(remaining.Count, tile);
      }
    }

    /// <summary> 用一张不在牌山里的牌替换牌山最后的牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceLast(GameTile tile) {
      return Replace(remaining.Count - 1, tile);
    }

    /// <summary> 将一张牌作为第i张里宝牌 </summary>
    public void PlaceUradora(int i, GameTile tile) {
      Swap(uradoras, i, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换第i张里宝牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceUradora(int i, GameTile tile) {
      return Replace(uradoras, i, tile);
    }

    /// <summary> 将一张牌作为第i张宝牌 </summary>
    public void PlaceDora(int i, GameTile tile) {
      Swap(doras, i, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换第i张宝牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceDora(int i, GameTile tile) {
      return Replace(doras, i, tile);
    }

    /// <summary> 将一张牌作为第i张岭上牌（从0开始） </summary>
    public void PlaceRinshan(int i, GameTile tile) {
      Swap(rinshan, rinshan.Count - i - 1, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换第i张岭上牌（从0开始） </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceRinshan(int i, GameTile tile) {
      return Replace(rinshan, rinshan.Count - i - 1, tile);
    }

    /// <summary> 将一张牌放到岭上牌最前 </summary>
    public void PlaceRinshanFirst(GameTile tile) {
      PlaceRinshan(0, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换岭上牌最前的牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceRinshanFirst(GameTile tile) {
      return Replace(rinshan, rinshan.Count - 1, tile);
    }

    /// <summary> 将一张牌放到岭上牌最后 </summary>
    public void PlaceRinshanLast(GameTile tile) {
      PlaceRinshan(rinshan.Count - 1, tile);
    }

    /// <summary> 用一张不在牌山里的牌替换岭上牌最后的牌 </summary>
    /// <returns>被替换的牌</returns>
    public GameTile ReplaceRinshanLast(GameTile tile) {
      return Replace(rinshan, 0, tile);
    }
  }
}
