using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public enum TileSource {
        None, Wanpai, Wall, Hand, Discard, Chi, Pon, Kan, Ron
    }

    public class GameTile : IComparable<GameTile> {
        public Tile tile = Tile.Empty;
        /// <summary> 来自哪个玩家（吃碰杠等） </summary>
        public Player fromPlayer;
        /// <summary> 当前归属于哪个玩家 </summary>
        public Player player;
        /// <summary> 弃牌的时间戳 </summary>
        public int discardTime = -1;
        /// <summary> 是否是公开牌 </summary>
        public bool visible = false;
        /// <summary> 是否是立直宣告牌 </summary>
        public bool riichi = false;
        /// <summary> 是否是自摸 </summary>
        public bool IsTsumo => fromPlayer == null;
        public TileSource source = TileSource.Hand;

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
