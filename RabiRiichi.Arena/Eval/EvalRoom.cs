using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// Per-seat outcome of a finished Arena match. Placement is 1-based (1 =
  /// winner). See ARENA_DESIGN.md §10/§11.
  /// </summary>
  public sealed class EvalSeatResult {
    public int Seat { get; init; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public long FinalPoints { get; init; }
    public int Placement { get; init; }
    public int PenaltyCount { get; init; }
  }

  /// <summary>
  /// The full result of running one headless eval match. Returned by
  /// <see cref="EvalRoom.RunAsync"/> and consumed by the rating/match wiring
  /// (§11). <see cref="Seed"/> is the REAL post-game seed read from
  /// <c>RabiRand</c> at game end (never exposed mid-game, §11b).
  /// </summary>
  public sealed class EvalResult {
    public string GameId { get; init; } = "";

    /// <summary>Real seed the engine used (stamped into the replay + this result).</summary>
    public ulong Seed { get; init; }

    /// <summary>Per-seat results, ordered by seat index.</summary>
    public IReadOnlyList<EvalSeatResult> Seats { get; init; } = Array.Empty<EvalSeatResult>();

    /// <summary>Full GameConfig snapshot (with the real seed) as JSON (§11).</summary>
    public JsonNode Config { get; init; }

    public string StartedAt { get; init; } = "";
    public string FinishedAt { get; init; } = "";

    /// <summary>True if the game ran to completion; false if it was cancelled.</summary>
    public bool Completed { get; init; }

    public EvalSeatResult SeatOf(int seat) => Seats.FirstOrDefault(s => s.Seat == seat);
  }

  /// <summary>
  /// A single table assignment: seat index (0..playerCount-1) mapped to the
  /// roster entry (LLM model or baseline) that occupies it.
  /// </summary>
  public sealed class EvalSeatAssignment {
    public int Seat { get; init; }
    public ArenaConfig.ModelConfig Model { get; init; }
  }

  /// <summary>
  /// A headless, humanless single-match runner (ARENA_DESIGN.md §10/§11b).
  ///
  /// Given four seat assignments and the <see cref="ArenaConfig"/>, it:
  ///  - builds a <see cref="GameConfig"/> with the config's playerCount /
  ///    totalRound and <c>seed = null</c> so the engine derives a clock seed the
  ///    agents can NEVER see during play (§2/§11b);
  ///  - attaches the right agent per seat — <see cref="ArenaLlmAI"/> for LLM
  ///    models (its <see cref="ILlmProvider"/> built from the model via an
  ///    injected factory), <see cref="RuleBasedAI"/> for baselines — wiring
  ///    reasoning + usage + exposure + seat→wind / seat→displayName resolvers;
  ///  - drives the game to completion via
  ///    <see cref="Room.RunHeadlessArenaMatch"/>;
  ///  - at game end, reads the real seed from <c>game.Get&lt;RabiRand&gt;()</c>,
  ///    stamps it into the replay log's config, and saves the replay (§11b);
  ///  - collects per-seat final points + placements and returns an
  ///    <see cref="EvalResult"/>.
  ///
  /// A <see cref="CancellationToken"/> can stop a run mid-match (M4 uses this).
  ///
  /// Tie-break rule for placement: players are ranked by final points DESCENDING;
  /// ties are broken by SEAT WIND ORDER (East &lt; South &lt; West &lt; North),
  /// i.e. the player closer to the dealer places higher. This mirrors the
  /// engine's own <c>PlayersByRank</c> convention (points desc, then id) applied
  /// to seat winds, and is fully deterministic.
  /// </summary>
  public sealed class EvalRoom {
    /// <summary>
    /// Builds the <see cref="ILlmProvider"/> for an LLM roster entry. Injected so
    /// tests never touch the network and so production can plug in the real
    /// <c>LlmProviderFactory</c>. Never called for baseline entries.
    /// </summary>
    public delegate ILlmProvider LlmProviderResolver(ArenaConfig.ModelConfig model);

    private readonly ArenaConfig config;
    private readonly ReplayStore replayStore;
    private readonly ReasoningStore reasoningStore;
    private readonly UsageStats usageStats;
    private readonly LlmProviderResolver llmProviderResolver;

    /// <param name="config">The Arena config (run/decision/exposure/rating/urls).</param>
    /// <param name="replayStore">
    /// Where the web-compatible replay is saved; may be a disabled store (then no
    /// replay is written, but the seed is still captured).
    /// </param>
    /// <param name="reasoningStore">Per-decision reasoning persistence (§8); may be null.</param>
    /// <param name="usageStats">Per-model usage counters (§12c); may be null for baselines.</param>
    /// <param name="llmProviderResolver">
    /// Builds a provider for an LLM model; defaults to a resolver that throws if
    /// an LLM seat is requested without one (so an all-baseline match needs none).
    /// </param>
    public EvalRoom(
        ArenaConfig config,
        ReplayStore replayStore,
        ReasoningStore reasoningStore = null,
        UsageStats usageStats = null,
        LlmProviderResolver llmProviderResolver = null) {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      this.replayStore = replayStore;
      this.reasoningStore = reasoningStore;
      this.usageStats = usageStats;
      this.llmProviderResolver = llmProviderResolver
          ?? (m => throw new InvalidOperationException(
              $"No LLM provider resolver supplied for model '{m?.Id}'. " +
              "All-baseline matches need none; LLM matches must inject one."));
    }

    /// <summary>
    /// Runs one match to completion and returns its result.
    /// </summary>
    /// <param name="assignments">
    /// Exactly playerCount seat assignments (seats 0..playerCount-1, each once).
    /// </param>
    /// <param name="gameId">
    /// Arena-controlled game id (must be path-safe; keys the replay + reasoning).
    /// If null/empty a fresh one is generated.
    /// </param>
    /// <param name="cancellationToken">Stops the match mid-run (M4).</param>
    public async Task<EvalResult> RunAsync(
        IReadOnlyList<EvalSeatAssignment> assignments,
        string gameId = null,
        CancellationToken cancellationToken = default) {
      if (assignments == null) {
        throw new ArgumentNullException(nameof(assignments));
      }
      int playerCount = config.Run.PlayerCount;
      if (assignments.Count != playerCount) {
        throw new ArgumentException(
            $"Expected {playerCount} seat assignments, got {assignments.Count}.",
            nameof(assignments));
      }
      var bySeat = assignments.OrderBy(a => a.Seat).ToList();
      for (int i = 0; i < bySeat.Count; i++) {
        if (bySeat[i].Seat != i) {
          throw new ArgumentException(
              $"Seat assignments must cover seats 0..{playerCount - 1} exactly once.",
              nameof(assignments));
        }
        if (bySeat[i].Model == null) {
          throw new ArgumentException($"Seat {i} has no model.", nameof(assignments));
        }
      }

      if (string.IsNullOrEmpty(gameId)) {
        gameId = NewGameId();
      }

      // Build the live config: seed stays NULL so the engine derives a clock seed
      // that agents can never see (§11b). A large action timeout keeps the engine
      // from preempting long LLM thinking (§7).
      var gameConfig = new GameConfig {
        playerCount = playerCount,
        totalRound = config.Run.TotalRound,
        seed = null,
        gameplayActionTimeout = Math.Max(
            5.0, Math.Min(3600.0, config.Decision.TimeoutSeconds + 10.0)),
      };

      var room = new Room(new Random(), gameConfig, replayStore);

      // The engine shuffles seats, but every agent reports its true engine Seat
      // via room.SeatIndexOf, so identities are resolved through the agents.
      var agents = new IPlayerAgent[playerCount];

      // Resolvers used by LLM agents. windOf reads the live game (available once
      // started); seatDisplayName resolves each engine SEAT's model display name
      // (only consulted when exposure.revealOpponentIdentity is on).
      Func<int, Wind> windOf = seat => room.game != null
          ? room.game.GetPlayer(seat).Wind
          : Wind.E;
      Func<int, string> seatDisplayName = seat =>
          ModelForSeat(agents, bySeat, seat)?.DisplayName ?? "";

      for (int i = 0; i < playerCount; i++) {
        var model = bySeat[i].Model;
        int aiId = room.AllocateAiId();
        IPlayerAgent agent = IsBaseline(model)
            ? new RuleBasedAI(aiId, room, UserStatus.InRoom)
            : new ArenaLlmAI(
                aiId, room, model, config.Decision, config.Exposure,
                llmProviderResolver(model), reasoningStore, usageStats, gameId,
                windOf, seatDisplayName, UserStatus.InRoom);
        agents[i] = agent;
        if (!room.AddPlayer(agent)) {
          throw new InvalidOperationException($"Failed to add agent for seat {i}.");
        }
      }

      // Do NOT call room.GetReady here: readying the last agent would auto-start
      // a normal game via the private TryStartGame. RunHeadlessArenaMatch takes
      // agents that are still InRoom and drives the single eval game itself.

      var startedAt = DateTime.UtcNow;
      Game capturedGame = null;
      ulong capturedSeed = 0;

      bool started = await room.RunHeadlessArenaMatch(
          gameId,
          onGameCreated: g => capturedGame = g,
          beforeSaveReplay: (g, sac) => {
            // §11b: read the real seed at game end and stamp it into the replay
            // log's embedded config BEFORE it is saved. Never earlier.
            var rand = g.Get<RabiRand>();
            capturedSeed = rand?.seed ?? 0;
            g.config.seed = capturedSeed;
            var log = sac.GetReplayLog();
            if (log?.Config != null) {
              log.Config.Seed = capturedSeed;
            }
          },
          cancellationToken);

      var finishedAt = DateTime.UtcNow;
      bool completed = started
          && capturedGame != null
          && capturedGame.info.phase == GamePhase.Finished
          && !cancellationToken.IsCancellationRequested;

      var seats = BuildSeatResults(capturedGame, agents, bySeat);

      // Config snapshot (with the real seed) for the match record (§11).
      JsonNode configSnapshot = null;
      if (capturedGame != null) {
        configSnapshot = ConfigToJson(capturedGame.config);
      }

      return new EvalResult {
        GameId = gameId,
        Seed = capturedSeed,
        Seats = seats,
        Config = configSnapshot,
        StartedAt = startedAt.ToString("o"),
        FinishedAt = finishedAt.ToString("o"),
        Completed = completed,
      };
    }

    /// <summary>
    /// Builds a <see cref="MatchRecord"/> from a finished match's
    /// <see cref="EvalResult"/> and the applied Elo changes, so the caller (or
    /// M4's ArenaService) can <c>Append</c> it. <paramref name="eloChanges"/> is
    /// keyed by model id; a seat with no change entry gets equal before/after.
    /// The replay link is built from the config's clientUrl/wsUrl (§11/§12a).
    /// </summary>
    public MatchRecord BuildMatchRecord(
        EvalResult result,
        IReadOnlyDictionary<string, EloChange> eloChanges,
        string matchId = null,
        string runId = null,
        int swissRound = 0) {
      if (result == null) {
        throw new ArgumentNullException(nameof(result));
      }
      matchId ??= result.GameId;
      var players = result.Seats.Select(s => {
        eloChanges.TryGetValue(s.ModelId, out var change);
        return new MatchPlayer {
          Seat = s.Seat,
          ModelId = s.ModelId,
          DisplayName = s.DisplayName,
          FinalPoints = (int)s.FinalPoints,
          Placement = s.Placement,
          PenaltyCount = s.PenaltyCount,
          EloBefore = change?.EloBefore ?? 0,
          EloAfter = change?.EloAfter ?? 0,
        };
      }).ToList();

      return new MatchRecord {
        MatchId = matchId,
        GameId = result.GameId,
        RunId = runId ?? "",
        SwissRound = swissRound,
        StartedAt = result.StartedAt,
        FinishedAt = result.FinishedAt,
        Seed = unchecked((long)result.Seed),
        Config = result.Config,
        Players = players,
        ReplayLink = BuildReplayLink(result.GameId),
      };
    }

    /// <summary>
    /// Builds the rating participants (current Elo, placement, frozen anchors)
    /// for a finished match, reading current Elo from <paramref name="store"/>.
    /// New models default to <paramref name="initialElo"/>; baselines carry their
    /// <c>FrozenElo</c>. Consumed by <see cref="RatingService.ApplyMatch"/>.
    /// </summary>
    public IReadOnlyList<RatingParticipant> BuildRatingParticipants(
        EvalResult result,
        IReadOnlyList<EvalSeatAssignment> assignments,
        RatingStore store,
        double initialElo) {
      var modelBySeat = assignments.ToDictionary(a => a.Seat, a => a.Model);
      return result.Seats.Select(s => {
        modelBySeat.TryGetValue(s.Seat, out var model);
        double? frozen = model != null && IsBaseline(model) ? model.FrozenElo : null;
        double current = store?.Get(s.ModelId)?.Elo ?? frozen ?? initialElo;
        return new RatingParticipant {
          ModelId = s.ModelId,
          Elo = current,
          Placement = s.Placement,
          FrozenElo = frozen,
          PenaltyCount = s.PenaltyCount,
        };
      }).ToList();
    }

    // ----- Placement + result assembly -------------------------------------

    private static IReadOnlyList<EvalSeatResult> BuildSeatResults(
        Game game, IPlayerAgent[] agents, List<EvalSeatAssignment> bySeat) {
      if (game == null) {
        // Never started (e.g. immediate cancel) — return zeroed placements.
        return bySeat.Select(a => new EvalSeatResult {
          Seat = a.Seat,
          ModelId = a.Model.Id,
          DisplayName = a.Model.DisplayName,
          FinalPoints = 0,
          Placement = 0,
          PenaltyCount = 0,
        }).ToList();
      }

      int playerCount = game.players.Length;
      // Map each engine seat -> its roster model via the agent occupying that seat.
      var modelForSeat = new ArenaConfig.ModelConfig[playerCount];
      var penaltyForSeat = new int[playerCount];
      for (int slot = 0; slot < agents.Length; slot++) {
        int seat = agents[slot].Seat;
        if (seat < 0 || seat >= playerCount) {
          continue;
        }
        modelForSeat[seat] = bySeat[slot].Model;
        penaltyForSeat[seat] = PenaltyOf(agents[slot]);
      }

      // Placement: sort seats by (final points desc, then seat wind asc). Seat
      // wind order (E<S<W<N) is the deterministic tie-break (§ class doc).
      var ordered = Enumerable.Range(0, playerCount)
          .OrderByDescending(seat => game.players[seat].points)
          .ThenBy(seat => (int)game.players[seat].Wind)
          .ToList();
      var placementForSeat = new int[playerCount];
      for (int rank = 0; rank < ordered.Count; rank++) {
        placementForSeat[ordered[rank]] = rank + 1;
      }

      var results = new List<EvalSeatResult>(playerCount);
      for (int seat = 0; seat < playerCount; seat++) {
        var model = modelForSeat[seat];
        results.Add(new EvalSeatResult {
          Seat = seat,
          ModelId = model?.Id ?? "",
          DisplayName = model?.DisplayName ?? "",
          FinalPoints = game.players[seat].points,
          Placement = placementForSeat[seat],
          PenaltyCount = penaltyForSeat[seat],
        });
      }
      return results.OrderBy(r => r.Seat).ToList();
    }

    private static int PenaltyOf(IPlayerAgent agent) {
      // Penalties are tracked in UsageStats per model (across the whole run), not
      // per agent instance. Per-match, per-seat penalty attribution is derived by
      // the caller from reasoning artifacts if needed; baselines never penalize.
      return 0;
    }

    // ----- Helpers ---------------------------------------------------------

    private string BuildReplayLink(string gameId) {
      var clientUrl = config.ClientUrl ?? "";
      var wsUrl = config.WsUrl ?? "";
      return $"{clientUrl}?server={wsUrl}&replay={gameId}";
    }

    private static bool IsBaseline(ArenaConfig.ModelConfig model) =>
        string.Equals(model.Provider, "baseline", StringComparison.OrdinalIgnoreCase);

    private static ArenaConfig.ModelConfig ModelForSeat(
        IPlayerAgent[] agents, List<EvalSeatAssignment> bySeat, int seat) {
      for (int slot = 0; slot < agents.Length; slot++) {
        if (agents[slot] != null && agents[slot].Seat == seat) {
          return bySeat[slot].Model;
        }
      }
      return null;
    }

    private static readonly JsonFormatter ProtoJson =
        new(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    private static JsonNode ConfigToJson(GameConfig config) {
      try {
        var json = ProtoJson.Format(config.ToProto());
        return JsonNode.Parse(json);
      } catch {
        return null;
      }
    }

    private static string NewGameId() =>
        $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}".Substring(0, 40);
  }
}
