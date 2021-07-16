using System.Collections.Generic;

namespace RabiRiichi.Riichi {
    public class Wall {
        public Tiles drawn;
        public Tiles remaining;
        public Tiles doraIndicators;

        public Wall(string tiles = "", string doraInds = "") {
            drawn = new Tiles(tiles);
            doraIndicators = new Tiles(doraInds);
            remaining = Tiles.All;
            remaining.Remove(drawn);
            remaining.Remove(doraIndicators);
        }

        public bool Draw(Tile tile) {
            if (!remaining.Contains(tile)) {
                return false;
            }
            remaining.Remove(tile);
            drawn.Add(tile);
            return true;
        }

        public void RevealDoraIndicator(Tile tile) {
            doraIndicators.Add(tile);
            remaining.Remove(tile);
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
