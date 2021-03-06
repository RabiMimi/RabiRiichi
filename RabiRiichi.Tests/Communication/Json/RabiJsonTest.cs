using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Json;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Tests.Helper;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Tests.Communication.Json {
    [TestClass]
    public class RabiJsonTest {
        #region Test Classes
        [RabiMessage]
        private abstract class BaseRabiMessage { }

        private class RabiTestNestedMessage : BaseRabiMessage, IRabiPlayerMessage {
            public int playerId { get; init; }
            [RabiBroadcast] public string broadcastMessage = "nested broadcast";
            [RabiPrivate] public string privateMessage = "nest private";

            public int notIncluded = 233;

            public RabiTestNestedMessage(int playerId) {
                this.playerId = playerId;
            }
        }

        private class RabiTestMessage : BaseRabiMessage, IRabiPlayerMessage {
            public int playerId { get; init; }
            [RabiBroadcast] public string broadcastMessage { get; set; } = "broadcast";
            [RabiPrivate] public readonly string privateMessage = "private";
            [RabiBroadcast] public RabiTestNestedMessage broadcastNested;
            [RabiPrivate] public RabiTestNestedMessage privateNested;
            [RabiPrivate] public RabiTestNestedMessage nullNested = null;

            public RabiTestMessage(int playerId) {
                this.playerId = playerId;
                broadcastNested = new RabiTestNestedMessage(playerId);
                privateNested = new RabiTestNestedMessage(playerId);
            }
        }

        private class NotRabiMessage {
            public string message { get; set; } = "not rabi message";
        }

        private class NotIWithPlayer : BaseRabiMessage {
            [RabiBroadcast]
            public string message { get; set; } = "not IWithPlayer";
        }

        private class InvalidNotIWithPlayer : BaseRabiMessage {
            [RabiPrivate]
            public string privateMessage { get; set; } = "invalid private message without player";
        }

        private class ValidMessageNonPublicSet : BaseRabiMessage {
            [RabiBroadcast]
            [JsonInclude]
            public virtual string message { get; protected set; } = "valid message with private set";
        }

        [RabiPrivate]
        private class InvalidRabiPrivateNotIWithPlayer : BaseRabiMessage {
            [RabiBroadcast]
            public string message { get; set; } = "invalid rabi private message without player";
        }

        [RabiPrivate]
        private class RabiPrivateWithPlayer : BaseRabiMessage, IRabiPlayerMessage {
            public int playerId { get; init; }
            [RabiBroadcast]
            public string message { get; set; } = "rabi private message with player";

            public RabiPrivateWithPlayer(int playerId) {
                this.playerId = playerId;
            }
        }

        private class InheritedMessage : ValidMessageNonPublicSet {
            public override string message { get; protected set; } = "inherited message";
            [RabiBroadcast] public string newMessage = "new inherited message";
        }

        private class GameTileMessage : BaseRabiMessage {
            [RabiBroadcast] public readonly GameTile tile;
            [RabiBroadcast] public readonly Tiles tiles;

            public GameTileMessage(GameTile tile, Tiles tiles) {
                this.tile = tile;
                this.tiles = tiles;
            }
        }

        [RabiIgnore]
        private class IgnoredMessage : RabiTestMessage {
            public IgnoredMessage() : base(0) { }
        }
        #endregion

        [TestMethod]
        public void TestSuccessNested() {
            var message = new RabiTestMessage(0);
            var json = RabiJson.Stringify(message, 0);
            // Parse by current player
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.broadcastMessage, parsed.GetProperty("broadcastMessage").GetString());
            Assert.AreEqual(message.privateMessage, parsed.GetProperty("privateMessage").GetString());
            Assert.AreEqual(message.broadcastNested.broadcastMessage, parsed.GetProperty("broadcastNested").GetProperty("broadcastMessage").GetString());
            Assert.AreEqual(message.privateNested.privateMessage, parsed.GetProperty("privateNested").GetProperty("privateMessage").GetString());
            Assert.AreEqual(JsonValueKind.Null, parsed.GetProperty("nullNested").ValueKind);
            Assert.IsFalse(parsed.TryGetProperty("notIncluded", out _));

            // Parse by the other player
            var partialJson = RabiJson.Stringify(message, 1);
            var partialParsed = RabiJson.Parse<JsonElement>(partialJson, 1);
            Assert.AreEqual(message.broadcastMessage, partialParsed.GetProperty("broadcastMessage").GetString());
            Assert.IsFalse(partialParsed.TryGetProperty("privateMessage", out _));
            Assert.AreEqual(message.broadcastNested.broadcastMessage, partialParsed.GetProperty("broadcastNested").GetProperty("broadcastMessage").GetString());
            Assert.IsFalse(partialParsed.GetProperty("broadcastNested").TryGetProperty("privateMessage", out _));
            Assert.IsFalse(partialParsed.TryGetProperty("privateNested", out _));
        }

        [TestMethod]
        public void TestSuccessNotRabiMessage() {
            var message = new NotRabiMessage();
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestSuccessNotIWithPlayer() {
            var message = new NotIWithPlayer();
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestInvalidNotIWithPlayer() {
            var message = new InvalidNotIWithPlayer();
            var except = Assert.ThrowsException<JsonException>(
                () => RabiJson.Stringify(message, 0),
                "is not IWithPlayer but has properties or fields that are not broadcastable");
        }

        [TestMethod]
        public void TestSuccessNonPublicSet() {
            var message = new ValidMessageNonPublicSet();
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestInvalidRabiPrivateNotIWithPlayer() {
            var message = new InvalidRabiPrivateNotIWithPlayer();
            var except = Assert.ThrowsException<JsonException>(
                () => RabiJson.Stringify(message, 0),
                "is not IWithPlayer but marked as RabiPrivate");
        }

        [TestMethod]
        public void TestSuccessRabiPrivateWithPlayer() {
            var message = new RabiPrivateWithPlayer(0);
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestNullRabiPrivateWithPlayerOtherPlayer() {
            var message = new RabiPrivateWithPlayer(1);
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual(parsed.ValueKind, JsonValueKind.Null);
        }

        [TestMethod]
        public void TestSuccessInheritedMessage() {
            var messages = new List<ValidMessageNonPublicSet> { new InheritedMessage() };
            var json = RabiJson.Stringify(messages, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0)[0];
            Assert.AreEqual(messages[0].message, parsed.GetProperty("message").GetString());
            Assert.AreEqual((messages[0] as InheritedMessage).newMessage, parsed.GetProperty("newMessage").GetString());
        }

        [TestMethod]
        public void TestSuccessGameTileMessage() {
            var tiles = new Tiles("123s123m123p");
            var message = new GameTileMessage(new GameTile(new Tile("r5s"), -1) {
                source = TileSource.Wall,
                discardInfo = new DiscardInfo(
                    new Player(1, TestHelper.CreateGame()), DiscardReason.Draw, 0),
            }, tiles);
            var json = RabiJson.Stringify(message, 0);
            var parsed = RabiJson.Parse<JsonElement>(json, 0);
            Assert.AreEqual("r5s", parsed.GetProperty("tile").GetProperty("tile").GetString());
            Assert.AreEqual((int)TileSource.Wall, parsed.GetProperty("tile").GetProperty("source").GetInt32());
            Assert.AreEqual(1, parsed.GetProperty("tile").GetProperty("discardInfo").GetProperty("from").GetInt32());
            Assert.AreEqual("123s123m123p", parsed.GetProperty("tiles").GetString());
            var parsedMsg = RabiJson.Parse<GameTileMessage>(json, 0);
            Assert.AreEqual(message.tile.tile, parsedMsg.tile.tile);
            CollectionAssert.AreEqual(message.tiles, parsedMsg.tiles);
        }

        [TestMethod]
        public void TestIgnoredMessage() {
            var message = new IgnoredMessage();
            var json = RabiJson.Stringify(message, 0);
            Assert.AreEqual("null", json);
        }
    }
}