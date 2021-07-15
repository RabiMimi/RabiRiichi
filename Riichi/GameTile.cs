namespace RabiRiichi.Riichi {
    public enum TileSource {
        OpenDora, Wall, Hand, Chi, Pon, Kan
    }
    public class GameTile {
        public Tile tile;
        public byte fromPlayer;
        public byte player;
        public bool visible;
        public TileSource source;
    }
}
