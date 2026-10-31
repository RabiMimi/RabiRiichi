using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// An <see cref="ILlmProvider"/> that always picks menu choice 0 (choice ids
  /// are 0-based and dense, so that is always legal) with a per-call rationale.
  /// Lets a whole match run against a "model" with no network and no script.
  /// </summary>
  internal sealed class ConstantLlmProvider : ILlmProvider {
    private int calls;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Openai;
    public int CallCount => calls;

    public Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens, CancellationToken ct) {
      int n = Interlocked.Increment(ref calls);
      return Task.FromResult(LlmResult.Ok(
          $"{{\"action\": 0, \"rationale\": \"decision {n}\"}}"));
    }
  }

  /// <summary>
  /// Covers the replay-aligned reasoning track (ARENA_DESIGN.md §9): the pure
  /// projection, and — end to end, over a real headless match with one LLM seat —
  /// that each anchor really points at the move it explains in the saved replay.
  /// </summary>
  [TestClass]
  public class ReasoningTrackTest {
    private string workspace;

    // A full match is heavy (seconds, well past the per-test timeout), so it runs
    // once in ClassInit and the assertions below read its artifacts — mirroring
    // EvalRoomTest's shared-match pattern.
    private const string MatchGameId = "track-e2e-1";
    private static string matchWorkspace;
    private static ReplayStore matchReplayStore;
    private static ReasoningStore matchReasoning;
    private static EvalResult matchResult;
    private static ConstantLlmProvider matchProvider;
    private static string llmModelId;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _) {
      matchWorkspace = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_track_match_{Guid.NewGuid():N}");
      var replayDir = Path.Combine(matchWorkspace, "replays");
      Directory.CreateDirectory(replayDir);
      matchReplayStore = new ReplayStore(new ReplayOptions(replayDir, null));
      matchReasoning = new ReasoningStore(matchWorkspace);

      var config = new ArenaConfig {
        WorkspaceDir = matchWorkspace,
        AdminPassword = "pw",
      };
      config.GameRules.TotalRound = 1;
      config.GameRules.PlayerCount = 4;
      config.Decision.TimeoutSeconds = 30;
      var llm = new ArenaConfig.ModelConfig {
        Id = "llm-seat",
        DisplayName = "Fake LLM",
        Provider = "openai",
        Model = "fake",
        Enabled = true,
      };
      llmModelId = llm.Id;
      config.Models.Add(llm);
      for (char c = 'a'; c <= 'c'; c++) {
        config.Models.Add(new ArenaConfig.ModelConfig {
          Id = $"base-{c}",
          DisplayName = $"BASE-{char.ToUpperInvariant(c)}",
          Provider = "baseline",
          Variant = "default",
          FrozenElo = 1500,
          Enabled = true,
        });
      }

      matchProvider = new ConstantLlmProvider();
      var room = new EvalRoom(
          config, matchReplayStore, matchReasoning,
          new UsageStats(matchWorkspace), _ => matchProvider);
      var table = config.Models
          .Select((m, i) => new EvalSeatAssignment { Seat = i, Model = m })
          .ToList();
      matchResult = await room.RunAsync(table, MatchGameId, CancellationToken.None);
    }

    [ClassCleanup]
    public static void ClassCleanup() {
      if (Directory.Exists(matchWorkspace)) {
        try { Directory.Delete(matchWorkspace, recursive: true); } catch { }
      }
    }

    [TestInitialize]
    public void Setup() {
      workspace = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_track_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspace);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspace)) {
        try { Directory.Delete(workspace, recursive: true); } catch { }
      }
    }

    // ----- Pure projection --------------------------------------------------

    [TestMethod]
    public void Build_OrdersByEventIndexAndSkipsUnanchoredOrEmpty() {
      var store = new ReasoningStore(workspace);
      store.AppendTurn("g1", 0, Turn(eventIndex: 40, rationale: "late"));
      store.AppendTurn("g1", 0, Turn(eventIndex: 10, rationale: "early"));
      // No anchor (pre-existing transcript) and empty rationale: nothing to show.
      store.AppendTurn("g1", 0, Turn(eventIndex: -1, rationale: "unanchored"));
      store.AppendTurn("g1", 1, Turn(eventIndex: 10, rationale: "same tick, seat 1"));
      store.AppendTurn("g1", 1, Turn(eventIndex: 20, rationale: "   "));

      var track = ReasoningTrack.Build(
          store, "g1", new[] { 0, 1 }, seat => $"Model {seat}");

      CollectionAssert.AreEqual(
          new[] { "early", "same tick, seat 1", "late" },
          track.Select(e => e.Rationale).ToArray());
      CollectionAssert.AreEqual(
          new[] { 10, 10, 40 }, track.Select(e => e.EventIndex).ToArray());
      CollectionAssert.AreEqual(new[] { 0, 1, 0 }, track.Select(e => e.Seat).ToArray());
      Assert.AreEqual("Model 1", track[1].DisplayName);
    }

    [TestMethod]
    public void Build_ToleratesMissingInputs() {
      var store = new ReasoningStore(workspace);
      Assert.AreEqual(0, ReasoningTrack.Build(null, "g1", new[] { 0 }).Count);
      Assert.AreEqual(0, ReasoningTrack.Build(store, "../evil", new[] { 0 }).Count);
      Assert.AreEqual(0, ReasoningTrack.Build(store, "g1", null).Count);
      // Unknown game / seat: no transcript on disk, no entries, no throw.
      Assert.AreEqual(0, ReasoningTrack.Build(store, "g-none", new[] { 0, -1 }).Count);
    }

    private static ReasoningTurn Turn(int eventIndex, string rationale) => new() {
      EventIndex = eventIndex,
      Rationale = rationale,
      Valid = true,
    };

    // ----- End-to-end anchor alignment --------------------------------------

    /// <summary>
    /// Runs a real match with seat 0 played by an LLM agent, then checks every
    /// discard decision against the SAVED replay: the first discard at or after
    /// the recorded anchor must be that seat's. This is the property the replay
    /// viewer relies on, and it is what makes the anchor meaningful — an index
    /// that drifted would surface the rationale on someone else's move.
    /// </summary>
    [TestMethod]
    public void Anchors_PointAtTheDecidingSeatsNextDiscardInTheReplay() {
      Assert.IsTrue(matchResult.Completed, "match should run to completion");
      Assert.IsTrue(matchProvider.CallCount > 0, "the LLM seat should have been asked");

      // The engine shuffles seats, so find where the LLM agent actually sat.
      int llmSeat = matchResult.Seats.First(s => s.ModelId == llmModelId).Seat;

      var replay = matchReplayStore.GetReplay(MatchGameId);
      Assert.IsNotNull(replay);
      // Exactly the stream a replay viewer reconstructs: events only, in order.
      var events = replay.PlayerLogs[0].Logs
          .Where(l => l.Event != null)
          .Select(l => l.Event)
          .ToList();

      var turns = matchReasoning.ReadTurns(MatchGameId, llmSeat);
      Assert.IsTrue(turns.Count > 0, "the LLM seat should have persisted turns");
      Assert.IsTrue(turns.All(t => t.EventIndex >= 0 && t.EventIndex <= events.Count),
          "every turn should carry an in-range replay anchor");

      var discardTurns = turns
          .Where(t => t.ParsedAction.StartsWith("discard", StringComparison.Ordinal))
          .ToList();
      Assert.IsTrue(discardTurns.Count > 0, "the LLM seat should have discarded");

      foreach (var turn in discardTurns) {
        var nextDiscard = events
            .Skip(turn.EventIndex)
            .Select(e => e.DiscardTileEvent)
            .FirstOrDefault(d => d != null);
        Assert.IsNotNull(nextDiscard,
            $"turn {turn.TurnSeq} anchored at {turn.EventIndex} with no discard after it");
        Assert.AreEqual(llmSeat, nextDiscard.PlayerId,
            $"turn {turn.TurnSeq} anchored at {turn.EventIndex} points at seat "
                + $"{nextDiscard.PlayerId}'s discard, not the deciding seat's");
      }

      // And the projection the endpoint serves lines up with those turns.
      var track = ReasoningTrack.Build(
          matchReasoning, MatchGameId, Enumerable.Range(0, 4), seat => $"seat{seat}");
      Assert.AreEqual(turns.Count, track.Count,
          "every anchored turn with a rationale should reach the track");
      Assert.IsTrue(track.All(e => e.Seat == llmSeat));
      Assert.IsTrue(track.Zip(track.Skip(1)).All(p => p.First.EventIndex <= p.Second.EventIndex),
          "track must be in replay order");
    }
  }
}
