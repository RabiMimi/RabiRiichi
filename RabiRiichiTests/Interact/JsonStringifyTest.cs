using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichiTests.Interact {
    [TestClass]
    public class JsonStringifyTest {
        #region Test Classes
        [RabiMessage]
        private class RabiTestNestedMessage : IWithPlayer {
            public Player player { get; init; }
            [RabiBroadcast]
            public string broadcastMessage = "nested broadcast";
            [RabiPrivate]
            public string privateMessage = "nest private";

            public int notIncluded = 233;

            public RabiTestNestedMessage(Player player) {
                this.player = player;
            }
        }

        [RabiMessage]
        private class RabiTestMessage : IWithPlayer {
            public Player player { get; init; }
            [RabiBroadcast]
            public string broadcastMessage { get; set; } = "broadcast";
            [RabiPrivate]
            public readonly string privateMessage = "private";
            [RabiBroadcast]
            public RabiTestNestedMessage broadcastNested;
            [RabiPrivate]
            public RabiTestNestedMessage privateNested;

            public RabiTestMessage(Player player) {
                this.player = player;
                broadcastNested = new RabiTestNestedMessage(player);
                privateNested = new RabiTestNestedMessage(player);
            }
        }

        private class NotRabiMessage {
            public string message { get; set; } = "not rabi message";
        }

        [RabiMessage]
        private class NotIWithPlayer {
            [RabiBroadcast]
            public string message { get; set; } = "not IWithPlayer";
        }

        [RabiMessage]
        private class InvalidNotIWithPlayer {
            [RabiPrivate]
            public string privateMessage { get; set; } = "invalid private message without player";
        }

        [RabiMessage]
        private class ValidMessagePrivateSet {
            [RabiBroadcast]
            [JsonInclude]
            public string message { get; private set; } = "valid message with private set";
        }

        [RabiMessage]
        [RabiPrivate]
        private class InvalidRabiPrivateNotIWithPlayer {
            [RabiBroadcast]
            public string message { get; set; } = "invalid rabi private message without player";
        }

        [RabiMessage]
        [RabiPrivate]
        private class RabiPrivateWithPlayer : IWithPlayer {
            public Player player { get; init; }
            [RabiBroadcast]
            public string message { get; set; } = "rabi private message with player";

            public RabiPrivateWithPlayer(Player player) {
                this.player = player;
            }
        }

        private class InheritedMessage : ValidMessagePrivateSet {
            [RabiBroadcast] public string newMessage = "inherited message";
        }
        #endregion

        private readonly Player player0 = new(0, null);
        private readonly Player player1 = new(1, null);
        private readonly JsonStringify jsonStringify = new(new GameConfig {
            playerCount = 2,
        });

        [TestMethod]
        public void TestSucceedNested() {
            var message = new RabiTestMessage(player0);
            var json = jsonStringify.Stringify(message, 0);
            // Parse by current player
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.broadcastMessage, parsed.GetProperty("broadcastMessage").GetString());
            Assert.AreEqual(message.privateMessage, parsed.GetProperty("privateMessage").GetString());
            Assert.AreEqual(message.broadcastNested.broadcastMessage, parsed.GetProperty("broadcastNested").GetProperty("broadcastMessage").GetString());
            Assert.AreEqual(message.privateNested.privateMessage, parsed.GetProperty("privateNested").GetProperty("privateMessage").GetString());
            Assert.IsFalse(parsed.TryGetProperty("notIncluded", out _));

            // Parse by the other player
            var partialJson = jsonStringify.Stringify(message, 1);
            var partialParsed = jsonStringify.Parse<JsonElement>(partialJson, 1);
            Assert.AreEqual(message.broadcastMessage, partialParsed.GetProperty("broadcastMessage").GetString());
            Assert.IsFalse(partialParsed.TryGetProperty("privateMessage", out _));
            Assert.AreEqual(message.broadcastNested.broadcastMessage, partialParsed.GetProperty("broadcastNested").GetProperty("broadcastMessage").GetString());
            Assert.IsFalse(partialParsed.GetProperty("broadcastNested").TryGetProperty("privateMessage", out _));
            Assert.IsFalse(partialParsed.TryGetProperty("priavteNested", out _));
        }

        [TestMethod]
        public void TestSucceedNotRabiMessage() {
            var message = new NotRabiMessage();
            var json = jsonStringify.Stringify(message, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestSucceedNotIWithPlayer() {
            var message = new NotIWithPlayer();
            var json = jsonStringify.Stringify(message, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestInvalidNotIWithPlayer() {
            var message = new InvalidNotIWithPlayer();
            var except = Assert.ThrowsException<JsonException>(
                () => jsonStringify.Stringify(message, 0),
                "is not IWithPlayer but has properties or fields that are not broadcastable");
        }

        [TestMethod]
        public void TestSuccessPrivateSet() {
            var message = new ValidMessagePrivateSet();
            var json = jsonStringify.Stringify(message, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestInvalidRabiPrivateNotIWithPlayer() {
            var message = new InvalidRabiPrivateNotIWithPlayer();
            var except = Assert.ThrowsException<JsonException>(
                () => jsonStringify.Stringify(message, 0),
                "is not IWithPlayer but marked as RabiPrivate");
        }

        [TestMethod]
        public void TestSuccessRabiPrivateWithPlayer() {
            var message = new RabiPrivateWithPlayer(player0);
            var json = jsonStringify.Stringify(message, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(message.message, parsed.GetProperty("message").GetString());
        }

        [TestMethod]
        public void TestNullRabiPrivateWithPlayerOtherPlayer() {
            var message = new RabiPrivateWithPlayer(player1);
            var json = jsonStringify.Stringify(message, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0);
            Assert.AreEqual(parsed.ValueKind, JsonValueKind.Null);
        }

        [TestMethod]
        public void TestInheritedMessage() {
            var messages = new List<ValidMessagePrivateSet> { new InheritedMessage() };
            var json = jsonStringify.Stringify(messages, 0);
            var parsed = jsonStringify.Parse<JsonElement>(json, 0)[0];
            Assert.AreEqual(messages[0].message, parsed.GetProperty("message").GetString());
            Assert.AreEqual((messages[0] as InheritedMessage).newMessage, parsed.GetProperty("newMessage").GetString());
        }
    }
}