using RabiRiichi.Communication;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public enum TileSource {
        None,
        /// <summary> 王牌 </summary>
        Wanpai,
        /// <summary> 牌山 </summary>
        Wall,
        /// <summary> 手牌 </summary>
        Hand,
        /// <summary> 弃牌 </summary>
        Discard,
        /// <summary> 吃 </summary>
        Chi,
        /// <summary> 碰 </summary>
        Pon,
        /// <summary> 加杠 </summary>
        KaKan,
        /// <summary> 暗杠 </summary>
        AnKan,
        /// <summary> 大明杠 </summary>
        DaiMinKan,
    }

    public enum DiscardReason {
        None,
        /// <summary> 抽牌 </summary>
        Draw,
        /// <summary> 抽岭上 </summary>
        DrawRinshan,
        /// <summary> 吃 </summary>
        Chi,
        /// <summary> 碰 </summary>
        Pon,
    }

    public class DiscardInfo {
        /// <summary> 来自哪个玩家（吃碰杠等） </summary>
        public readonly Player fromPlayer;
        /// <summary> 弃牌原因 </summary>
        public readonly DiscardReason reason;
        /// <summary> 弃牌时间，与<see cref="GameInfo.timeStamp"/>同步 </summary>
        public readonly int discardTime;

        public DiscardInfo(Player fromPlayer, DiscardReason reason, int discardTime) {
            this.fromPlayer = fromPlayer;
            this.reason = reason;
            this.discardTime = discardTime;
        }
    }

    public class GameTile : IComparable<GameTile>, IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public Tile tile = Tile.Empty;
        [RabiBroadcast] public int? fromPlayerId => discardInfo?.fromPlayer.id;
        /// <summary> 当前归属于哪个玩家，摸切或副露时会被设置 </summary>
        public Player player;
        [RabiBroadcast] public int? playerId => player?.id;
        /// <summary> 弃牌信息 </summary>
        public DiscardInfo discardInfo;
        /// <summary> 该牌成为副露或暗杠的时间戳 </summary>
        public int formTime = -1;
        /// <summary> 是否是公开牌 </summary>
        // public bool visible = false;
        /// <summary> 是否是立直宣告牌 </summary>
        [RabiBroadcast] public bool riichi = false;
        /// <summary> 是否是自摸 </summary>
        public bool IsTsumo => discardInfo == null;
        [RabiBroadcast] public TileSource source = TileSource.Hand;

        /// <summary> 是否是万筒索 </summary>
        public bool IsMPS => tile.IsMPS;

        public GameTile(Tile tile) {
            this.tile = tile;
        }

        public int CompareTo(GameTile other) {
            return tile.CompareTo(other.tile);
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

    public class GameTiles : List<GameTile> {
        public TileSource source = TileSource.Hand;
        public GameTiles() { }
        public GameTiles(IEnumerable<GameTile> tiles) : base(tiles) { }
        public GameTiles(IEnumerable<Tile> tiles)
            : base(tiles.Select(tile => new GameTile(tile))) { }
        public Tiles ToTiles() {
            return new Tiles(this.Select(gameTile => gameTile.tile));
        }

        /// <summary> 判定两个搭子是否相同，赤宝牌视为相同牌 </summary>
        public virtual bool IsSame(GameTiles other) {
            if (this.Count != other.Count)
                return false;

            var thisTiles = this.ToTiles();
            var otherTiles = other.ToTiles();
            thisTiles.Sort();
            otherTiles.Sort();
            for (int i = 0; i < thisTiles.Count; i++) {
                if (!thisTiles[i].IsSame(otherTiles[i]))
                    return false;
            }
            return true;
        }

        /// <summary> 判定是否有给出的牌，赤宝牌视为相同牌 </summary>
        public bool HasTile(Tile tile) {
            return this.Any(t => t.tile.IsSame(tile));
        }

        public override string ToString() {
            var ret = this.ToTiles();
            ret.Sort();
            return ret.ToString();
        }
    }
}
