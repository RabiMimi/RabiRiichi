using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;


namespace RabiRiichiTests.Core {
    [TestClass]
    public class GameTest {
        private readonly Game game;

        public GameTest() {
            var config = new GameConfig {
                actionCenter = new JsonStringActionCenter(null)
            };
            game = new Game(config);
        }

        [TestMethod]
        public void TestGameExists() {
            Assert.IsNotNull(game);
        }
    }
}