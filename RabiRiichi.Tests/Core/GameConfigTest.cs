using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Core {
  [TestClass]
  public class GameConfigTest {
    [TestMethod]
    public void TestDefaultConfigIsValid() {
      var config = new GameConfig();
      // Should not throw
      config.Validate();
    }

    [TestMethod]
    public void TestInvalidPlayerCount() {
      var config = new GameConfig { playerCount = 1 };
      var ex = Assert.ThrowsException<InvalidGameConfigException>(() => config.Validate());
      Assert.AreEqual(GameConfigErrorType.InvalidPlayerCount, ex.ErrorType);

      config = new GameConfig { playerCount = 5 };
      ex = Assert.ThrowsException<InvalidGameConfigException>(() => config.Validate());
      Assert.AreEqual(GameConfigErrorType.InvalidPlayerCount, ex.ErrorType);
    }

    [TestMethod]
    public void TestInsufficientTiles() {
      // 4 players requires 13 * 4 + 15 = 67 tiles
      var config = new GameConfig { playerCount = 4 };
      
      // Create a list of 66 tiles
      var tiles = Enumerable.Range(0, 66).Select(i => new Tile(TileSuit.M, 1)).ToList();
      config.initialTiles = new Tiles(tiles);
      
      var ex = Assert.ThrowsException<InvalidGameConfigException>(() => config.Validate());
      Assert.AreEqual(GameConfigErrorType.InsufficientTiles, ex.ErrorType);
      Assert.AreEqual(66, ex.Parameters["count"]);
      Assert.AreEqual(4, ex.Parameters["players"]);
      Assert.AreEqual(67, ex.Parameters["min"]);

      // 67 tiles should be valid
      tiles = Enumerable.Range(0, 67).Select(i => new Tile(TileSuit.M, 1)).ToList();
      config.initialTiles = new Tiles(tiles);
      // Should not throw
      config.Validate();
    }

    [TestMethod]
    public void TestInsufficientTiles2Player() {
      // 2 players requires 13 * 2 + 15 = 41 tiles
      var config = new GameConfig { playerCount = 2 };
      
      // 40 tiles should be invalid
      var tiles = Enumerable.Range(0, 40).Select(i => new Tile(TileSuit.M, 1)).ToList();
      config.initialTiles = new Tiles(tiles);
      var ex = Assert.ThrowsException<InvalidGameConfigException>(() => config.Validate());
      Assert.AreEqual(GameConfigErrorType.InsufficientTiles, ex.ErrorType);

      // 41 tiles should be valid
      tiles = Enumerable.Range(0, 41).Select(i => new Tile(TileSuit.M, 1)).ToList();
      config.initialTiles = new Tiles(tiles);
      // Should not throw
      config.Validate();
    }
  }
}
