﻿using RabiRiichi.Communication;
using RabiRiichi.Generated.Core;
using System;

namespace RabiRiichi.Core {
    [RabiMessage]
    public class DiscardInfo {
        /// <summary> 哪个玩家的弃牌 </summary>
        public readonly Player fromPlayer;
        [RabiBroadcast] public readonly int from;
        /// <summary> 弃牌原因 </summary>
        [RabiBroadcast] public readonly DiscardReason reason;
        /// <summary> 弃牌时间戳 </summary>
        [RabiBroadcast] public readonly int time;

        public DiscardInfo(Player fromPlayer, DiscardReason reason, int time) {
            this.fromPlayer = fromPlayer;
            this.from = fromPlayer?.id ?? -1;
            this.reason = reason;
            this.time = time;
        }
    }

    [RabiMessage]
    public class GameTile : IComparable<GameTile> {
        internal class Refrigerator : IDisposable {
            public readonly GameTile gameTile;
            public readonly Tile tile;
            public readonly Player player;
            public readonly DiscardInfo discardInfo;
            public readonly int formTime;
            public readonly TileSource source;
            public Refrigerator(GameTile tile) {
                this.gameTile = tile;
                this.tile = tile.tile;
                this.player = tile.player;
                this.discardInfo = tile.discardInfo;
                this.formTime = tile.formTime;
                this.source = tile.source;
            }

            public void Dispose() {
                gameTile.tile = tile;
                gameTile.player = player;
                gameTile.discardInfo = discardInfo;
                gameTile.formTime = formTime;
                gameTile.source = source;
            }
        }

        [RabiBroadcast] public Tile tile = Tile.Empty;

        /// <summary> 随机获取的牌跟踪ID，保证一局内不重复，在进入牌山后重置 </summary>
        [RabiBroadcast] public int traceId;
        /// <summary> 当前归属于哪个玩家，摸切或副露时会被设置 </summary>
        public Player player;
        [RabiBroadcast] public int playerId => player?.id ?? -1;
        /// <summary> 弃牌信息 </summary>
        [RabiBroadcast] public DiscardInfo discardInfo;
        /// <summary> 该牌成为副露或暗杠的时间戳 </summary>
        [RabiBroadcast] public int formTime = -1;
        /// <summary> 是否是自摸 </summary>
        public bool IsTsumo => discardInfo == null;
        [RabiBroadcast] public TileSource source = TileSource.Hand;

        /// <summary> 是否是万筒索 </summary>
        public bool IsMPS => tile.IsMPS;

        public GameTile(Tile tile, int traceId) {
            this.tile = tile;
            this.traceId = traceId;
        }

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
        public bool IsSame(GameTile other) => tile.IsSame(other.tile);
        /// <summary> 是否是下一张牌，用于顺子计算 </summary>
        public bool NextIs(GameTile other) => tile.IsNext(other.tile);
        /// <summary> 是否是上一张牌，用于顺子计算 </summary>
        public bool PrevIs(GameTile other) => tile.IsPrev(other.tile);

        public override string ToString() {
            return tile.ToString();
        }
    }
}
