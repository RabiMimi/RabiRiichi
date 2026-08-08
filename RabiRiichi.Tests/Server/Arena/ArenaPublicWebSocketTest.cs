using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.WebSockets;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.Utils;
using RabiRiichi.Server.WebSockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for the /ws/public replay handler (ARENA_DESIGN.md §12d). These
  /// exercise the extracted, socket-free
  /// <see cref="ArenaPublicWebSocket.HandlePublicMessage"/> (decoded request →
  /// response DTO), so a stored replay round-trips and GetInfo returns the
  /// version fields the client validates — no Kestrel, no real socket required.
  /// </summary>
  [TestClass]
  public class ArenaPublicWebSocketTest {
    private string replayDir;
    private ReplayStore replayStore;
    private ArenaPublicWebSocket handler;

    [TestInitialize]
    public void Setup() {
      replayDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_ws_{Guid.NewGuid():N}");
      Directory.CreateDirectory(replayDir);
      replayStore = new ReplayStore(new ReplayOptions(replayDir, ttl: null));
      handler = new ArenaPublicWebSocket(
          replayStore, new InfoServiceImpl(NullLogger<InfoServiceImpl>.Instance));
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(replayDir)) {
        try { Directory.Delete(replayDir, recursive: true); } catch { }
      }
    }

    private static ClientMessageDto GetReplayMsg(string gameId, int id = 7) => new() {
      Id = id,
      ClientRequest = new ClientRequest { GetReplay = new GetReplayRequest { GameId = gameId } },
    };

    [TestMethod]
    public void TestGetReplayRoundTripsStoredReplay() {
      var gameId = "game-abc-1";
      var stored = new GameLogMsg { GameId = gameId, CreatedAtUnixMs = 12345 };
      replayStore.SaveReplay(gameId, stored);

      var reply = handler.HandlePublicMessage(GetReplayMsg(gameId, id: 42));

      Assert.IsNotNull(reply);
      Assert.AreEqual(42, reply.RespondTo);
      Assert.IsNotNull(reply.ServerResp?.Replay);
      Assert.AreEqual(gameId, reply.ServerResp.Replay.GameId);
      Assert.AreEqual(12345, reply.ServerResp.Replay.CreatedAtUnixMs);
    }

    [TestMethod]
    public void TestGetReplayMissingReturnsNotFoundError() {
      var reply = handler.HandlePublicMessage(GetReplayMsg("game-missing"));
      Assert.IsNotNull(reply);
      Assert.IsNotNull(reply.ServerResp?.ServerError);
      Assert.AreEqual("NotFound", reply.ServerResp.ServerError.Status);
    }

    [TestMethod]
    public void TestGetReplayInvalidIdReturnsInvalidArgumentError() {
      var reply = handler.HandlePublicMessage(GetReplayMsg("../evil"));
      Assert.IsNotNull(reply);
      Assert.IsNotNull(reply.ServerResp?.ServerError);
      Assert.AreEqual("InvalidArgument", reply.ServerResp.ServerError.Status);
    }

    [TestMethod]
    public void TestGetInfoReturnsServerVersionForClientCheck() {
      var msg = new ClientMessageDto {
        Id = 3,
        ClientRequest = new ClientRequest {
          GetInfo = new Google.Protobuf.WellKnownTypes.Empty(),
        },
      };

      var reply = handler.HandlePublicMessage(msg);

      Assert.IsNotNull(reply);
      Assert.AreEqual(3, reply.RespondTo);
      var info = reply.ServerResp?.GetInfo;
      Assert.IsNotNull(info);
      Assert.AreEqual(ServerConstants.SERVER_VERSION, info.ServerVersion);
      Assert.AreEqual(ServerConstants.MIN_CLIENT_VERSION, info.MinClientVersion);
    }

    [TestMethod]
    public void TestUnknownRequestReturnsNull() {
      // A version-check echo / unsupported public request needs no reply.
      Assert.IsNull(handler.HandlePublicMessage(new ClientMessageDto { Id = 1 }));
      Assert.IsNull(handler.HandlePublicMessage(null));
    }

    [TestMethod]
    public void TestHeartBeatIsAnswered() {
      // Unanswered pings make the client tear the socket down after ~15s.
      var reply = handler.HandlePublicMessage(new ClientMessageDto {
        Id = -4,
        ClientMsg = new ClientMsg { HeartBeatMsg = new TwoWayHeartBeatMsg { MaxId = 3 } },
      });

      Assert.IsNotNull(reply);
      Assert.AreEqual(-1, reply.Id);
      Assert.AreEqual(-4, reply.RespondTo);
      Assert.IsNotNull(reply.ServerMsg?.HeartBeatMsg);
      // No outgoing ids are assigned here, so nothing can be "missing".
      Assert.AreEqual(0, reply.ServerMsg.HeartBeatMsg.MaxId);
      Assert.AreEqual(0, reply.ServerMsg.HeartBeatMsg.RequestingIds.Count);
    }

    // ----- Full-connection tests (fake socket) -------------------------------

    /// <summary>
    /// Regression test for the replay page hanging: the web client's second
    /// concurrent fetchReplay only awaits the socket's <c>open</c>, not the
    /// handshake, so its GetReplay arrives BEFORE the version-check reply. That
    /// request must still be served, not silently dropped.
    /// </summary>
    [TestMethod]
    public async Task TestRequestPipelinedBeforeVersionReplyIsStillServed() {
      var gameId = "game-pipelined";
      replayStore.SaveReplay(gameId, new GameLogMsg { GameId = gameId });

      var socket = new FakeWebSocket(
          Encode(GetReplayMsg(gameId, id: 1)),
          Encode(VersionReply()));

      await handler.HandleConnectionAsync(
          new WebSocketAdapter(socket), CancellationToken.None);

      var sent = socket.SentMessages.Select(ServerMessageDto.Parser.ParseFrom).ToList();
      Assert.AreEqual(2, sent.Count, "expected the version check plus the replay");
      Assert.IsNotNull(sent[0].ServerMsg?.VersionCheckMsg);
      Assert.AreEqual(1, sent[1].RespondTo);
      Assert.AreEqual(gameId, sent[1].ServerResp?.Replay?.GameId);
    }

    [TestMethod]
    public async Task TestRequestAfterHandshakeIsServed() {
      var gameId = "game-ordered";
      replayStore.SaveReplay(gameId, new GameLogMsg { GameId = gameId });

      var socket = new FakeWebSocket(
          Encode(VersionReply()),
          Encode(GetReplayMsg(gameId, id: 5)));

      await handler.HandleConnectionAsync(
          new WebSocketAdapter(socket), CancellationToken.None);

      var sent = socket.SentMessages.Select(ServerMessageDto.Parser.ParseFrom).ToList();
      Assert.AreEqual(2, sent.Count);
      Assert.IsNotNull(sent[0].ServerMsg?.VersionCheckMsg);
      Assert.AreEqual(5, sent[1].RespondTo);
      Assert.AreEqual(gameId, sent[1].ServerResp?.Replay?.GameId);
    }

    [TestMethod]
    public async Task TestUnsupportedClientVersionServesNothing() {
      var gameId = "game-oldclient";
      replayStore.SaveReplay(gameId, new GameLogMsg { GameId = gameId });

      var socket = new FakeWebSocket(
          Encode(GetReplayMsg(gameId, id: 1)),
          Encode(VersionReply(clientVersion: "0.0.1")));

      await handler.HandleConnectionAsync(
          new WebSocketAdapter(socket), CancellationToken.None);

      // Buffering pipelined requests must not weaken the version gate: only the
      // server's own version check goes out, and the socket is closed.
      Assert.AreEqual(1, socket.SentMessages.Count);
      var sent = ServerMessageDto.Parser.ParseFrom(socket.SentMessages[0]);
      Assert.IsNotNull(sent.ServerMsg?.VersionCheckMsg);
      Assert.IsTrue(socket.CloseCalled);
    }

    private static ClientMessageDto VersionReply(
        string clientVersion = ServerConstants.MIN_CLIENT_VERSION) => new() {
          Id = 2,
          ClientMsg = new ClientMsg {
            VersionCheckMsg = new ClientVersionCheckMsg {
              Client = "test",
              ClientVersion = clientVersion,
              MinServerVersion = "0.0.0",
            },
          },
        };

    private static byte[] Encode(ClientMessageDto msg) => msg.ToByteArray();

    /// <summary>
    /// Minimal in-memory <see cref="WebSocket"/>: hands out the queued client
    /// frames in order, records everything written, then reports a clean close.
    /// Lets the full connection loop (handshake included) run without Kestrel.
    /// </summary>
    private sealed class FakeWebSocket(params byte[][] incoming) : WebSocket {
      private readonly Queue<byte[]> incoming = new(incoming);

      public List<byte[]> SentMessages { get; } = [];
      public bool CloseCalled { get; private set; }

      public override WebSocketCloseStatus? CloseStatus => null;
      public override string CloseStatusDescription => null;
      public override WebSocketState State { get; } = WebSocketState.Open;
      public override string SubProtocol => null;

      public override void Abort() { }
      public override void Dispose() { }

      public override Task CloseAsync(
          WebSocketCloseStatus closeStatus, string statusDescription,
          CancellationToken cancellationToken) {
        CloseCalled = true;
        return Task.CompletedTask;
      }

      public override Task CloseOutputAsync(
          WebSocketCloseStatus closeStatus, string statusDescription,
          CancellationToken cancellationToken) => CloseAsync(
              closeStatus, statusDescription, cancellationToken);

      public override Task<WebSocketReceiveResult> ReceiveAsync(
          ArraySegment<byte> buffer, CancellationToken cancellationToken) {
        if (incoming.Count == 0) {
          return Task.FromResult(new WebSocketReceiveResult(
              0, WebSocketMessageType.Close, true));
        }
        var next = incoming.Dequeue();
        next.CopyTo(buffer.Array, buffer.Offset);
        return Task.FromResult(new WebSocketReceiveResult(
            next.Length, WebSocketMessageType.Binary, true));
      }

      public override Task SendAsync(
          ArraySegment<byte> buffer, WebSocketMessageType messageType,
          bool endOfMessage, CancellationToken cancellationToken) {
        SentMessages.Add(buffer.ToArray());
        return Task.CompletedTask;
      }
    }
  }
}
