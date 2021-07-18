using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public enum TileSource {
        OpenDora, Wall, Hand, Discard, Chi, Pon, Kan, Ron
    }
    public class GameTile : IComparable<GameTile> {
        public Tile tile = Tile.Empty;
        /// <summary> 来自哪个玩家（吃碰杠等） </summary>
        public int fromPlayer = -1;
        /// <summary> 当前归属于哪个玩家 </summary>
        public int player = -1;
        /// <summary> 弃牌的时间戳 </summary>
        public int discardTime = -1;
        /// <summary> 是否是公开牌 </summary>
        public bool visible = false;
        /// <summary> 是否是立直牌 </summary>
        public bool riichi = false;
        /// <summary> 是否是自摸 </summary>
        public bool IsTsumo => fromPlayer < 0;
        public TileSource source = TileSource.Hand;

        /// <summary> 是否是万筒索 </summary>
        public bool IsMPS => tile.IsMPS;

        public int CompareTo(GameTile other) {
            return tile.CompareTo(other.tile);
        }

        /// <summary> 是否是相同的牌，赤dora视为相同 </summary>
        public bool IsSame(GameTile other) => tile.IsSame(other.tile);
        /// <summary> 是否是下一张牌，用于顺子计算 </summary>
        public bool NextIs(GameTile other) => tile.IsNext(other.tile);
        /// <summary> 是否是上一张牌，用于顺子计算 </summary>
        public bool PrevIs(GameTile other) => tile.IsPrev(other.tile);
    }
    public class GameTiles : List<GameTile> {
        public TileSource source = TileSource.Hand;
        public GameTiles() { }
        public GameTiles(IEnumerable<GameTile> tiles) : base(tiles) { }
        public GameTiles(IEnumerable<Tile> tiles)
            : base(tiles.Select(tile => new GameTile { tile = tile })) { }
        public Tiles ToTiles() {
            return new Tiles(this.Select(gameTile => gameTile.tile));
        }
        public bool IsKou => Count == 3
            && this[0].IsSame(this[1]) && this[1].IsSame(this[2]);
        public bool IsKan {
            get {
                if (Count != 4) return false;
                for (int i = 1; i < Count; i++) {
                    if (!this[i - 1].IsSame(this[i]))
                        return false;
                }
                return true;
            }
        }
        public bool IsShun {
            get {
                if (Count != 3) return false;
                var list = this.ToList();
                list.Sort();
                return list[0].NextIs(list[1]) && list[1].NextIs(list[2]);
            }
        }
        public bool IsJan => Count == 2 && this[0].IsSame(this[1]);

        /// <summary> 判定是否有给出的牌，赤宝牌视为相同牌 </summary>
        public bool HasTile(Tile tile) {
            return this.Any(t => t.tile.IsSame(tile));
        }
        public override string ToString() {
            var ret = new Tiles(this.Select(tile => tile.tile));
            ret.Sort();
            return ret.ToString();
        }
    }
}
