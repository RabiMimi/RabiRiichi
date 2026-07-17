using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents.Llm;
using System.Collections.Generic;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class TileNotationTest {
    [TestMethod]
    public void One_FormatsSuitsAndHonors() {
      Assert.AreEqual("1m", TileNotation.One(new Tile("1m")));
      Assert.AreEqual("5p", TileNotation.One(new Tile("5p")));
      Assert.AreEqual("9s", TileNotation.One(new Tile("9s")));
      Assert.AreEqual("1z", TileNotation.One(new Tile("1z")));
    }

    [TestMethod]
    public void One_RedFiveIsZero() {
      Assert.AreEqual("0p", TileNotation.One(new Tile("0p")));
    }

    [TestMethod]
    public void Group_SortsAndGroupsBySuit() {
      var tiles = new List<Tile> {
        new("3m"), new("1m"), new("2m"),
        new("9s"), new("5p"), new("1z"), new("1z"),
      };
      Assert.AreEqual("123m5p9s11z", TileNotation.Group(tiles));
    }

    [TestMethod]
    public void Group_EmptyIsEmptyString() {
      Assert.AreEqual("", TileNotation.Group(new List<Tile>()));
    }

    [TestMethod]
    public void Group_KeepsRedFiveDigit() {
      var tiles = new List<Tile> { new("4p"), new("0p"), new("6p") };
      Assert.AreEqual("406p", TileNotation.Group(tiles));
    }
  }
}
