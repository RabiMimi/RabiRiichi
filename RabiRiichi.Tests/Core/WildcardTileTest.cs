using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication.Json;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents.Llm;
using System;
using System.Text.Json;

namespace RabiRiichi.Tests.Core {
  [TestClass]
  public class WildcardTileTest {
    [TestMethod]
    public void WildcardNotationAndIdentityRoundTrip() {
      Assert.AreEqual(Tile.Wildcard, new Tile("*"));
      Assert.IsTrue(Tile.Wildcard.IsValid);
      Assert.IsTrue(Tile.Wildcard.IsWildcard);
      Assert.IsFalse(Tile.Wildcard.Is19Z);
      Assert.IsFalse(Tile.Wildcard.IsMPS);
      Assert.AreEqual(Tile.Empty, Tile.Wildcard.NextDora);
      Assert.AreEqual(Tile.Wildcard, new Tile(Tile.Wildcard.Val));
      const string text = "*123m**405p*7z*";
      Assert.AreEqual(text.Replace("0", "r5"), new Tiles(text).ToString());
      Assert.ThrowsException<ArgumentException>(() => new Tiles("12*"));
      Assert.ThrowsException<ArgumentException>(() => new Tiles("r*"));
      Assert.IsFalse(new Tile(TileSuit.Wildcard, 2).IsValid);
    }

    [TestMethod]
    public void WildcardJsonProtoAndServerNotationRoundTrip() {
      var options = new JsonSerializerOptions();
      options.Converters.Add(new TileJsonConverter());
      options.Converters.Add(new TilesJsonConverter());
      Assert.AreEqual(Tile.Wildcard, JsonSerializer.Deserialize<Tile>(JsonSerializer.Serialize(Tile.Wildcard, options), options));
      var tiles = new Tiles("123m**");
      Assert.AreEqual(tiles.ToString(), JsonSerializer.Deserialize<Tiles>(JsonSerializer.Serialize(tiles, options), options).ToString());
      var msg = ProtoConverters.ConvertGameTile(new(Tile.Wildcard, 123), true);
      Assert.AreEqual((int)Tile.Wildcard.Val, msg.Tile);
      Assert.AreEqual(123, msg.TraceId);
      Assert.AreEqual("*", TileNotation.One(Tile.Wildcard));
      Assert.AreEqual("123m **", TileNotation.Group(tiles));
    }
  }
}
