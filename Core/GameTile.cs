using RabiRiichi.Communication;
using RabiRiichi.Generated.Core;
using System;

namespace RabiRiichi.Core {
  [RabiMessage]
  public class DiscardInfo(Player fromPlayer, DiscardReason reason, int time, int jun = 0) {
    /// <summary> 哪个玩家的弃牌 </summary>
    public readonly Player fromPlayer = fromPlayer;
    [RabiBroadcast] public readonly int from = fromPlayer?.id ?? -1;
    /// <summary> 弃牌原因 </summary>
    [RabiBroadcast] public readonly DiscardReason reason = reason;
    /// <summary> 弃牌时间戳 </summary>
    [RabiBroadcast] public readonly int time = time;
    /// <summary> 弃牌时的巡目（从1开始）。手切/摸切可由此与牌的 drawnJun 比较推断 </summary>
    [RabiBroadcast] public readonly int jun = jun;
  }

  [RabiMessage]
  public class GameTile(Tile tile, int traceId) : IComparable<GameTile> {
    internal class Refrigerator(GameTile tile) : IDisposable {
      public readonly GameTile gameTile = tile;
      public readonly Tile tile = tile.tile;
      public readonly Player player = tile.player;
      public readonly DiscardInfo discardInfo = tile.discardInfo;
      public readonly int formTime = tile.formTime;
      public readonly int drawnJun = tile.drawnJun;
      public readonly int formJun = tile.formJun;
      public readonly TileSource source = tile.source;

      public void Dispose() {
        gameTile.tile = tile;
        gameTile.player = player;
        gameTile.discardInfo = discardInfo;
        gameTile.formTime = formTime;
        gameTile.drawnJun = drawnJun;
        gameTile.formJun = formJun;
        gameTile.source = source;
      }
    }

    [RabiBroadcast] public Tile tile = tile;

    /// <summary> 随机获取的牌跟踪ID，保证一局内不重复，在进入牌山后重置 </summary>
    [RabiBroadcast] public int traceId = traceId;
    /// <summary> 当前归属于哪个玩家，摸切或副露时会被设置 </summary>
    public Player player;
    [RabiBroadcast] public int playerId => player?.id ?? -1;
    /// <summary> 弃牌信息 </summary>
    [RabiBroadcast] public DiscardInfo discardInfo;
    /// <summary> 该牌成为副露或暗杠的时间戳 </summary>
    [RabiBroadcast] public int formTime = -1;
    /// <summary> 该牌被摸进手牌时摸牌者的巡目（从1开始），0表示从未被摸进手牌 </summary>
    [RabiBroadcast] public int drawnJun = 0;
    /// <summary>该牌从手牌构成副露或暗杠时的巡目（从1开始），0表示未从手牌构成副露</summary>
    [RabiBroadcast] public int formJun = 0;
    /// <summary> 是否是自摸 </summary>
    public bool IsTsumo => discardInfo == null;
    [RabiBroadcast] public TileSource source = TileSource.Hand;

    /// <summary> 是否是万筒索 </summary>
    public bool IsMPS => tile.IsMPS;

    public int CompareTo(GameTile other) {
      return tile.CompareTo(other.tile);
    }

    /// <summary>
    /// 暂时保存当前牌的信息，并在之后还原
    /// </summary>
    internal Refrigerator Freeze(bool shouldFreeze = true) {
      return shouldFreeze ? new Refrigerator(this) : null;
    }

    /// <summary> 是否是相同的牌，赤dora视为相同 </summary>
    public bool IsSame(GameTile other) {
      return tile.IsSame(other.tile);
    }

    /// <summary> 是否是下一张牌，用于顺子计算 </summary>
    public bool NextIs(GameTile other) {
      return tile.IsNext(other.tile);
    }

    /// <summary> 是否是上一张牌，用于顺子计算 </summary>
    public bool PrevIs(GameTile other) {
      return tile.IsPrev(other.tile);
    }

    public override string ToString() {
      return tile.ToString();
    }
  }
}
