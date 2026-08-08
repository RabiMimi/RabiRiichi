using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.IO;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class ReasoningStoreTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_reason_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try {
          Directory.Delete(workspaceDir, recursive: true);
        } catch {
          // ignore
        }
      }
    }

    private static ReasoningMeta MakeMeta(string gameId, int seat) => new() {
      GameId = gameId,
      Seat = seat,
      ModelId = "gpt-x",
      Provider = "openai",
      Model = "gpt-x",
      SystemPrompt = "You are a mahjong player.",
      CreatedAt = "2024-01-01T00:00:00Z",
    };

    private static ReasoningTurn MakeTurn(int seq) => new() {
      TurnSeq = seq,
      PromptDelta = "recent events + menu " + seq,
      Reasoning = "thinking " + seq,
      RawResponse = "{\"action\":...}",
      ParsedAction = "discard 1m",
      Rationale = "cutting isolated tile",
      Valid = true,
      Attempts = 1,
      Penalized = false,
      PromptTokens = 100,
      CompletionTokens = 20,
      LatencyMs = 1500,
      Timestamp = "2024-01-01T00:0" + seq + ":00Z",
    };

    [TestMethod]
    public void TestWriteMetaOnce() {
      var store = new ReasoningStore(workspaceDir);
      Assert.IsTrue(store.WriteMeta(MakeMeta("game-1", 0)));

      // Second write is a no-op and does not overwrite.
      var second = MakeMeta("game-1", 0);
      second.ModelId = "changed";
      Assert.IsFalse(store.WriteMeta(second));

      var read = store.ReadMeta("game-1", 0);
      Assert.IsNotNull(read);
      Assert.AreEqual("gpt-x", read.ModelId);
      Assert.AreEqual("You are a mahjong player.", read.SystemPrompt);
    }

    [TestMethod]
    public void TestReadMetaMissingReturnsNull() {
      var store = new ReasoningStore(workspaceDir);
      Assert.IsNull(store.ReadMeta("game-1", 0));
    }

    [TestMethod]
    public void TestAppendTurnsInOrder() {
      var store = new ReasoningStore(workspaceDir);
      store.WriteMeta(MakeMeta("game-1", 2));
      store.AppendTurn("game-1", 2, MakeTurn(1));
      store.AppendTurn("game-1", 2, MakeTurn(2));
      store.AppendTurn("game-1", 2, MakeTurn(3));

      var turns = store.ReadTurns("game-1", 2);
      Assert.AreEqual(3, turns.Count);
      Assert.AreEqual(1, turns[0].TurnSeq);
      Assert.AreEqual(2, turns[1].TurnSeq);
      Assert.AreEqual(3, turns[2].TurnSeq);
      Assert.AreEqual("cutting isolated tile", turns[0].Rationale);
      Assert.AreEqual("recent events + menu 2", turns[1].PromptDelta);
    }

    [TestMethod]
    public void TestAppendTurnWithoutMetaStillWorks() {
      // Appending is independent of meta existing (meta is optional preamble).
      var store = new ReasoningStore(workspaceDir);
      store.AppendTurn("game-9", 0, MakeTurn(1));
      Assert.AreEqual(1, store.ReadTurns("game-9", 0).Count);
    }

    [TestMethod]
    public void TestErrorFieldRoundTrips() {
      var store = new ReasoningStore(workspaceDir);
      var turn = MakeTurn(1);
      turn.Valid = false;
      turn.Penalized = true;
      turn.Attempts = 3;
      turn.Error = "validation failed: illegal discard";
      store.AppendTurn("game-1", 0, turn);

      var read = store.ReadTurns("game-1", 0)[0];
      Assert.IsFalse(read.Valid);
      Assert.IsTrue(read.Penalized);
      Assert.AreEqual(3, read.Attempts);
      Assert.AreEqual("validation failed: illegal discard", read.Error);
    }

    [TestMethod]
    public void TestReadTurnsMissingReturnsEmpty() {
      var store = new ReasoningStore(workspaceDir);
      Assert.AreEqual(0, store.ReadTurns("game-1", 0).Count);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestPathSafetyRejectsBadGameId() {
      var store = new ReasoningStore(workspaceDir);
      store.WriteMeta(MakeMeta("../evil", 0));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestPathSafetyRejectsBadGameIdOnAppend() {
      var store = new ReasoningStore(workspaceDir);
      store.AppendTurn("bad/slash", 0, MakeTurn(1));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TestPathSafetyRejectsNegativeSeat() {
      var store = new ReasoningStore(workspaceDir);
      store.WriteMeta(MakeMeta("game-1", -1));
    }

    [TestMethod]
    public void TestIdValidationHelpers() {
      Assert.IsTrue(ReasoningStore.IsValidGameId("game-1"));
      Assert.IsTrue(ReasoningStore.IsValidGameId("abcABC123"));
      Assert.IsFalse(ReasoningStore.IsValidGameId(""));
      Assert.IsFalse(ReasoningStore.IsValidGameId("../evil"));
      Assert.IsFalse(ReasoningStore.IsValidGameId("has space"));
      Assert.IsFalse(ReasoningStore.IsValidGameId("bad/slash"));
      Assert.IsTrue(ReasoningStore.IsValidSeat(0));
      Assert.IsFalse(ReasoningStore.IsValidSeat(-1));
    }
  }
}
