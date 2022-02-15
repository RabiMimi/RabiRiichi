using RabiRiichi.Riichi;

namespace RabiRiichi.Action {

    public class SelectTileAction : UserAction<Tile> {
        public Tiles tiles;

        public SelectTileAction(Player player, Tiles tiles) : base(player) {
            this.tiles = tiles;
        }
    }
}