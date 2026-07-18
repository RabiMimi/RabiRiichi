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
      Assert.AreEqual("1z(East wind)", TileNotation.One(new Tile("1z")));
      Assert.AreEqual("4z(North wind)", TileNotation.One(new Tile("4z")));
      Assert.AreEqual("5z(White dragon)", TileNotation.One(new Tile("5z")));
      Assert.AreEqual("4z(北・風牌)", TileNotation.One(new Tile("4z"), "ja"));
      Assert.AreEqual("5z(白・三元牌)", TileNotation.One(new Tile("5z"), "ja"));
      Assert.AreEqual("4z(北风牌)", TileNotation.One(new Tile("4z"), "zhs"));
      Assert.AreEqual("5z(白，三元牌)", TileNotation.One(new Tile("5z"), "zhs"));
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
      Assert.AreEqual(
          "123m5p9s 1z(East wind) 1z(East wind)",
          TileNotation.Group(tiles));
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
