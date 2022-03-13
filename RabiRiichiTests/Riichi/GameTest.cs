using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;


namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class GameTest {
        private readonly Game game;

        public GameTest() {
            var config = new GameConfig();
            game = new Game(config);
        }

        [TestMethod]
        public void TestGameExists() {
            Assert.IsNotNull(game);
        }
    }
}