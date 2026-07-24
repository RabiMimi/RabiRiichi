using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.WebSockets;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.Utils;
using System;
using System.IO;

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
  }
}
