using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi {
    public enum TileSource {
        OpenDora, Wall, Hand, Discard, Chi, Pon, Kan
    }
    public class GameTile : IComparable<GameTile> {
        public Tile tile = Tile.Empty;
        public sbyte fromPlayer = -1;
        public sbyte player = -1;
        public bool visible = false;
        public bool riichi = false;
        public TileSource source = TileSource.Hand;

        public bool IsMPS => tile.IsMPS;

        public int CompareTo(GameTile other) {
            return tile.CompareTo(other.tile);
        }

        public bool IsSame(GameTile other) => tile.IsSame(other.tile);
        public bool NextIs(GameTile other) => tile.NextIs(other.tile);
        public bool PrevIs(GameTile other) => tile.PrevIs(other.tile);
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
    }
}
