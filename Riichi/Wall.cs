using System.Collections.Generic;

namespace RabiRiichi.Riichi {
    public class Wall {
        public Tiles drawn;
        public Tiles remaining;

        public Wall(string tiles = "") {
            drawn = new Tiles(tiles);
            remaining = Tiles.All;
            remaining.Remove(drawn);
        }

        public bool Draw(Tile tile) {
            if (!remaining.Contains(tile)) {
                return false;
            }
            remaining.Remove(tile);
            drawn.Add(tile);
            return true;
        }

        public bool Draw(IEnumerable<Tile> tiles) {
            foreach (var tile in tiles) {
                if (!Draw(tile))
                    return false;
            }
            return true;
        }
    }
}
