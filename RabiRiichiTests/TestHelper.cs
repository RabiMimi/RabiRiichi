using RabiRiichi.Riichi;

namespace RabiRiichiTests {
    static class TestHelper {
        public static Hand CreateHand(string str) {
            return new Hand {
                hand = new GameTiles(new Tiles(str))
            };
        }
    }
}
