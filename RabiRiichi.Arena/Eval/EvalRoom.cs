using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;
using RabiRiichi.Utils;

namespace RabiRiichi.Arena.Eval {
  public sealed class LiveSeatProgress {
    public int Seat { get; init; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public long Points { get; init; }
    public bool InRiichi { get; init; }
  }

  public sealed class LiveMatchProgress {
    public string GameId { get; init; } = "";
    public string RoundLabel { get; init; } = "";
    public int WallRemaining { get; init; }
    public int TurnCount { get; init; }
    public string StartedAt { get; init; } = "";
    public IReadOnlyList<LiveSeatProgress> Seats { get; init; } = Array.Empty<LiveSeatProgress>();
  }

  public sealed class EvalSeatResult {
    public int Seat { get; init; }
    public string ModelId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Provider { get; init; } = "";
    public long FinalPoints { get; init; }
    public int Placement { get; init; }

    public int RoundsPlayed { get; init; }
    public int AgariCount { get; init; }
    public int TsumoCount { get; init; }
    public int RonCount { get; init; }
    public int DealInCount { get; init; }
    public int DealInPoints { get; init; }
    public int TotalWinPoints { get; init; }
    public int TotalWinHan { get; init; }
    public int MaxWinPoints { get; init; }
    public int RiichiCount { get; init; }
    public int OpenHandCount { get; init; }
    public int CallCount { get; init; }
    public int KanCount { get; init; }
    public int NukiCount { get; init; }
    public int TenpaiAtDrawCount { get; init; }
    public int WildcardWinCount { get; init; }
    public Dictionary<string, int> YakuCounts { get; init; } = new();

    public int DecisionsCount { get; init; }
    public int AutoDecisionsCount { get; init; }
    public int ValidFirstTryCount { get; init; }
    public int RetryCount { get; init; }
    public int PenaltyCount { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long ReasoningTokens { get; init; }
    public long TotalLatencyMs { get; init; }
    public long MaxLatencyMs { get; init; }
  }

  public sealed class EvalResult {
    public string GameId { get; init; } = "";
    public ulong Seed { get; init; }
    public IReadOnlyList<EvalSeatResult> Seats { get; init; } = Array.Empty<EvalSeatResult>();
    public IReadOnlyList<MatchRoundSummary> Rounds { get; init; } = Array.Empty<MatchRoundSummary>();
    public int RoundsPlayed { get; init; }
    public int ExhaustiveDraws { get; init; }
    public int AbortedDraws { get; init; }
    public int PlayerCount { get; init; }
    public int TotalRound { get; init; }
    public string TileSetPreset { get; init; } = "Regular";
    public bool HasWildcards { get; init; }
    public int WildcardCount { get; init; }
    public long DurationMs { get; init; }
    public JsonNode Config { get; init; }
    public string StartedAt { get; init; } = "";
    public string FinishedAt { get; init; } = "";
    public bool Completed { get; init; }

    public EvalSeatResult SeatOf(int seat) => Seats.FirstOrDefault(s => s.Seat == seat);
  }

  public sealed class EvalSeatAssignment {
    public int Seat { get; init; }
    public ArenaConfig.ModelConfig Model { get; init; }
  }

  /// <summary>
  /// Headless single-match Arena runner with full custom ruleset/wildcard/koyaku support,
  /// live table progress reporting, rich Mahjong event telemetry, and post-game replay
  /// DisplayName + seed stamping.
  /// </summary>
  public sealed class EvalRoom {
    public delegate ILlmProvider LlmProviderResolver(ArenaConfig.ModelConfig model);

    private readonly ArenaConfig config;
    private readonly ReplayStore replayStore;
    private readonly ReasoningStore reasoningStore;
    private readonly UsageStats usageStats;
    private readonly LlmProviderResolver llmProviderResolver;

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
              $"No LLM provider resolver supplied for model '{m?.Id}'."));
    }

    public async Task<EvalResult> RunAsync(
        IReadOnlyList<EvalSeatAssignment> assignments,
        string gameId = null,
        CancellationToken cancellationToken = default,
        Action<LiveMatchProgress> onProgress = null) {
      if (assignments == null) throw new ArgumentNullException(nameof(assignments));
      var rules = config.GameRules ?? new ArenaConfig.GameRulesConfig();
      int playerCount = rules.PlayerCount;
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

      double actionTimeout = Math.Clamp((config.Decision?.TimeoutSeconds ?? 60) + 15.0, 5.0, 3600.0);
      var gameConfig = rules.BuildGameConfig(actionTimeout);
      int wildcardCount = gameConfig.initialTiles?.Count(t => t.IsWildcard) ?? 0;

      var room = new Room(new Random(), gameConfig, replayStore);
      var agents = new IPlayerAgent[playerCount];

      Func<int, Wind> windOf = seat => room.game != null
          ? room.game.GetPlayer(seat).Wind
          : Wind.E;
      Func<int, string> seatDisplayName = seat =>
          ModelForSeat(agents, bySeat, seat)?.DisplayName ?? "";

      var promptsCfg = config.Prompts ?? new ArenaConfig.PromptConfig();
      var decisionCfg = config.Decision ?? new ArenaConfig.DecisionConfig();

      for (int i = 0; i < playerCount; i++) {
        var model = bySeat[i].Model;
        int aiId = room.AllocateAiId();
        IPlayerAgent agent = model.IsBaseline
            ? new ArenaBaselineAI(
                aiId,
                room,
                model.Variant,
                promptsCfg.RevealOpponentIdentity ? model.DisplayName : null,
                UserStatus.InRoom)
            : new ArenaLlmAI(
                aiId,
                room,
                model,
                decisionCfg,
                promptsCfg,
                llmProviderResolver(model),
                reasoningStore,
                usageStats,
                gameId,
                windOf,
                seatDisplayName,
                UserStatus.InRoom);
        agents[i] = agent;
        if (!room.AddPlayer(agent)) {
          throw new InvalidOperationException($"Failed to add agent for seat {i}.");
        }
      }

      var startedAt = DateTime.UtcNow;
      var startedIso = startedAt.ToString("o");
      var sw = Stopwatch.StartNew();
      Game capturedGame = null;
      ulong capturedSeed = 0;

      var tracker = new MatchEventTracker(playerCount);

      void EmitProgress(Game g) {
        if (onProgress == null || g == null) return;
        try {
          var seatsSnap = new List<LiveSeatProgress>(playerCount);
          for (int s = 0; s < playerCount; s++) {
            var m = ModelForSeat(agents, bySeat, s);
            seatsSnap.Add(new LiveSeatProgress {
              Seat = s,
              ModelId = m?.Id ?? "",
              DisplayName = m?.DisplayName ?? $"Seat {s + 1}",
              Points = g.players[s].points,
              InRiichi = g.players[s].hand.riichi,
            });
          }
          onProgress(new LiveMatchProgress {
            GameId = gameId,
            RoundLabel = FormatRoundLabel(g),
            WallRemaining = g.wall?.NumRemaining ?? 0,
            TurnCount = tracker.TotalTurns,
            StartedAt = startedIso,
            Seats = seatsSnap,
          });
        } catch {
          // Progress reporting must never interrupt gameplay.
        }
      }

      bool started = await room.RunHeadlessArenaMatch(
          gameId,
          onGameCreated: g => {
            capturedGame = g;
            g.onGodViewEvent += ev => {
              tracker.OnEvent(g, ev);
              if (ev is BeginGameEvent or DiscardTileEvent or AgariEvent or RyuukyokuEvent) {
                EmitProgress(g);
              }
            };
          },
          beforeSaveReplay: (g, sac) => {
            var rand = g.Get<RabiRand>();
            capturedSeed = rand?.seed ?? 0;
            g.config.seed = capturedSeed;
            var log = sac.GetReplayLog();
            if (log != null) {
              if (log.Config != null) {
                log.Config.Seed = capturedSeed;
              }
              foreach (var p in log.Players) {
                var m = ModelForSeat(agents, bySeat, p.Seat);
                if (m != null && !string.IsNullOrWhiteSpace(m.DisplayName)) {
                  p.Nickname = m.DisplayName;
                }
              }
            }
          },
          cancellationToken);

      sw.Stop();
      var finishedAt = DateTime.UtcNow;
      bool completed = started
          && capturedGame != null
          && capturedGame.info.phase == GamePhase.Finished
          && !cancellationToken.IsCancellationRequested;

      var seats = BuildSeatResults(capturedGame, agents, bySeat, tracker);
      JsonNode configSnapshot = capturedGame != null ? ConfigToJson(capturedGame.config) : null;

      return new EvalResult {
        GameId = gameId,
        Seed = capturedSeed,
        Seats = seats,
        Rounds = tracker.Rounds,
        RoundsPlayed = tracker.RoundsPlayed,
        ExhaustiveDraws = tracker.ExhaustiveDraws,
        AbortedDraws = tracker.AbortedDraws,
        PlayerCount = playerCount,
        TotalRound = rules.TotalRound,
        TileSetPreset = rules.TileSet?.Preset ?? "Regular",
        HasWildcards = wildcardCount > 0,
        WildcardCount = wildcardCount,
        DurationMs = sw.ElapsedMilliseconds,
        Config = configSnapshot,
        StartedAt = startedIso,
        FinishedAt = finishedAt.ToString("o"),
        Completed = completed,
      };
    }

    public MatchRecord BuildMatchRecord(
        EvalResult result,
        IReadOnlyDictionary<string, EloChange> eloChanges,
        string matchId = null,
        string runId = null,
        int swissRound = 0,
        string batchLabel = "") {
      if (result == null) throw new ArgumentNullException(nameof(result));
      matchId ??= result.GameId;
      var players = result.Seats.Select(s => {
        eloChanges.TryGetValue(s.ModelId, out var change);
        return new MatchPlayer {
          Seat = s.Seat,
          ModelId = s.ModelId,
          DisplayName = s.DisplayName,
          Provider = s.Provider,
          FinalPoints = (int)s.FinalPoints,
          Placement = s.Placement,
          EloBefore = change?.EloBefore ?? 0,
          EloAfter = change?.EloAfter ?? 0,
          RoundsPlayed = s.RoundsPlayed,
          AgariCount = s.AgariCount,
          TsumoCount = s.TsumoCount,
          RonCount = s.RonCount,
          DealInCount = s.DealInCount,
          DealInPoints = s.DealInPoints,
          TotalWinPoints = s.TotalWinPoints,
          TotalWinHan = s.TotalWinHan,
          MaxWinPoints = s.MaxWinPoints,
          RiichiCount = s.RiichiCount,
          OpenHandCount = s.OpenHandCount,
          CallCount = s.CallCount,
          KanCount = s.KanCount,
          NukiCount = s.NukiCount,
          TenpaiAtDrawCount = s.TenpaiAtDrawCount,
          WildcardWinCount = s.WildcardWinCount,
          YakuCounts = new Dictionary<string, int>(s.YakuCounts),
          DecisionsCount = s.DecisionsCount,
          AutoDecisionsCount = s.AutoDecisionsCount,
          ValidFirstTryCount = s.ValidFirstTryCount,
          RetryCount = s.RetryCount,
          PenaltyCount = s.PenaltyCount,
          PromptTokens = s.PromptTokens,
          CompletionTokens = s.CompletionTokens,
          ReasoningTokens = s.ReasoningTokens,
          TotalLatencyMs = s.TotalLatencyMs,
          MaxLatencyMs = s.MaxLatencyMs,
        };
      }).ToList();

      return new MatchRecord {
        MatchId = matchId,
        GameId = result.GameId,
        RunId = runId ?? "",
        SwissRound = swissRound,
        BatchLabel = batchLabel ?? "",
        StartedAt = result.StartedAt,
        FinishedAt = result.FinishedAt,
        DurationMs = result.DurationMs,
        Seed = unchecked((long)result.Seed),
        PlayerCount = result.PlayerCount,
        TotalRound = result.TotalRound,
        TileSetPreset = result.TileSetPreset,
        HasWildcards = result.HasWildcards,
        WildcardCount = result.WildcardCount,
        RoundsPlayed = result.RoundsPlayed,
        ExhaustiveDraws = result.ExhaustiveDraws,
        AbortedDraws = result.AbortedDraws,
        Config = result.Config,
        Players = players,
        Rounds = result.Rounds.ToList(),
      };
    }

    public IReadOnlyList<RatingParticipant> BuildRatingParticipants(
        EvalResult result,
        IReadOnlyList<EvalSeatAssignment> assignments,
        RatingStore store,
        double initialElo) {
      var modelBySeat = assignments.ToDictionary(a => a.Seat, a => a.Model);
      return result.Seats.Select(s => {
        // Resolve the model from the seat's ModelId (since seats shuffle on game start).
        var model = assignments.Select(a => a.Model).FirstOrDefault(m => m.Id == s.ModelId)
            ?? modelBySeat.GetValueOrDefault(s.Seat);
        double? frozen = model != null && model.IsBaseline ? model.FrozenElo : null;
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

    private static IReadOnlyList<EvalSeatResult> BuildSeatResults(
        Game game,
        IPlayerAgent[] agents,
        List<EvalSeatAssignment> bySeat,
        MatchEventTracker tracker) {
      if (game == null) {
        return bySeat.Select(a => new EvalSeatResult {
          Seat = a.Seat,
          ModelId = a.Model.Id,
          DisplayName = a.Model.DisplayName,
          Provider = a.Model.Provider,
        }).ToList();
      }

      int playerCount = game.players.Length;
      var modelForSeat = new ArenaConfig.ModelConfig[playerCount];
      var agentForSeat = new IPlayerAgent[playerCount];
      for (int slot = 0; slot < agents.Length; slot++) {
        int seat = agents[slot].Seat;
        if (seat < 0 || seat >= playerCount) continue;
        modelForSeat[seat] = bySeat[slot].Model;
        agentForSeat[seat] = agents[slot];
      }

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
        var llm = agentForSeat[seat] as ArenaLlmAI;
        var st = tracker.StatsOf(seat);
        results.Add(new EvalSeatResult {
          Seat = seat,
          ModelId = model?.Id ?? "",
          DisplayName = model?.DisplayName ?? "",
          Provider = model?.Provider ?? "",
          FinalPoints = game.players[seat].points,
          Placement = placementForSeat[seat],
          RoundsPlayed = tracker.RoundsPlayed,
          AgariCount = st.AgariCount,
          TsumoCount = st.TsumoCount,
          RonCount = st.RonCount,
          DealInCount = st.DealInCount,
          DealInPoints = st.DealInPoints,
          TotalWinPoints = st.TotalWinPoints,
          TotalWinHan = st.TotalWinHan,
          MaxWinPoints = st.MaxWinPoints,
          RiichiCount = st.RiichiCount,
          OpenHandCount = st.OpenHandCount,
          CallCount = st.CallCount,
          KanCount = st.KanCount,
          NukiCount = st.NukiCount,
          TenpaiAtDrawCount = st.TenpaiAtDrawCount,
          WildcardWinCount = st.WildcardWinCount,
          YakuCounts = new Dictionary<string, int>(st.YakuCounts),
          DecisionsCount = llm?.DecisionsCount ?? 0,
          AutoDecisionsCount = llm?.AutoDecisionsCount ?? 0,
          ValidFirstTryCount = llm?.ValidFirstTryCount ?? 0,
          RetryCount = llm?.RetryCount ?? 0,
          PenaltyCount = llm?.PenaltyCount ?? 0,
          PromptTokens = llm?.PromptTokens ?? 0,
          CompletionTokens = llm?.CompletionTokens ?? 0,
          ReasoningTokens = llm?.ReasoningTokens ?? 0,
          TotalLatencyMs = llm?.TotalLatencyMs ?? 0,
          MaxLatencyMs = llm?.MaxLatencyMs ?? 0,
        });
      }
      return results.OrderBy(r => r.Seat).ToList();
    }

    private static string FormatRoundLabel(Game g) {
      string wind = g.info.wind switch {
        Wind.E => "East",
        Wind.S => "South",
        Wind.W => "West",
        Wind.N => "North",
        _ => g.info.wind.ToString(),
      };
      int kyoku = g.info.round % Math.Max(1, g.config.playerCount) + 1;
      return g.info.honba > 0 ? $"{wind} {kyoku} (H{g.info.honba})" : $"{wind} {kyoku}";
    }

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
        $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{Guid.NewGuid():N}"[..40];

    /// <summary>
    /// Aggregates per-seat and per-round Mahjong statistics directly from engine events.
    /// </summary>
    private sealed class MatchEventTracker {
      public sealed class SeatStats {
        public int AgariCount;
        public int TsumoCount;
        public int RonCount;
        public int DealInCount;
        public int DealInPoints;
        public int TotalWinPoints;
        public int TotalWinHan;
        public int MaxWinPoints;
        public int RiichiCount;
        public int OpenHandCount;
        public int CallCount;
        public int KanCount;
        public int NukiCount;
        public int TenpaiAtDrawCount;
        public int WildcardWinCount;
        public readonly Dictionary<string, int> YakuCounts = new(StringComparer.Ordinal);
      }

      private readonly int playerCount;
      private readonly SeatStats[] stats;
      private readonly bool[] openedInCurrentRound;
      private string currentRoundLabel = "East 1";
      private MatchRoundSummary pendingRound;
      private bool roundConcluded;

      public int RoundsPlayed { get; private set; }
      public int ExhaustiveDraws { get; private set; }
      public int AbortedDraws { get; private set; }
      public int TotalTurns { get; private set; }
      public List<MatchRoundSummary> Rounds { get; } = new();

      public MatchEventTracker(int playerCount) {
        this.playerCount = playerCount;
        stats = Enumerable.Range(0, playerCount).Select(_ => new SeatStats()).ToArray();
        openedInCurrentRound = new bool[playerCount];
      }

      public SeatStats StatsOf(int seat) =>
          seat >= 0 && seat < playerCount ? stats[seat] : new SeatStats();

      public void OnEvent(Game g, EventBase ev) {
        switch (ev) {
          case BeginGameEvent:
            RoundsPlayed++;
            Array.Clear(openedInCurrentRound);
            roundConcluded = false;
            pendingRound = null;
            currentRoundLabel = FormatRoundLabel(g);
            break;

          case DiscardTileEvent:
            TotalTurns++;
            break;

          case SetRiichiEvent riichi when ValidSeat(riichi.playerId):
            stats[riichi.playerId].RiichiCount++;
            break;

          case ClaimTileEvent claim when ValidSeat(claim.playerId):
            stats[claim.playerId].CallCount++;
            openedInCurrentRound[claim.playerId] = true;
            break;

          case KanEvent kan when ValidSeat(kan.playerId):
            stats[kan.playerId].KanCount++;
            if (kan.kanSource != TileSource.Ankan) {
              openedInCurrentRound[kan.playerId] = true;
            }
            break;

          case NukiDoraEvent nuki when ValidSeat(nuki.playerId):
            stats[nuki.playerId].NukiCount++;
            break;

          case AgariEvent agari: {
            FinalizeOpenHandsOnce();
            bool isTsumo = agari.isTsumo;
            int fromSeat = agari.agariInfos.fromPlayer;
            if (!isTsumo && ValidSeat(fromSeat)) {
              stats[fromSeat].DealInCount++;
            }

            var winners = new List<int>();
            var yakuNames = new List<string>();
            int topHan = 0, topFu = 0, topYakuman = 0;
            bool anyWildcard = false;

            foreach (var info in agari.agariInfos) {
              if (!ValidSeat(info.playerId)) continue;
              winners.Add(info.playerId);
              var s = stats[info.playerId];
              s.AgariCount++;
              if (isTsumo) s.TsumoCount++; else s.RonCount++;

              bool usedWild = info.WildcardAssignments().Any();
              if (usedWild) {
                anyWildcard = true;
                s.WildcardWinCount++;
              }

              if (info.scores != null) {
                int han = info.scores.result?.han ?? 0;
                int fu = info.scores.result?.fu ?? 0;
                int ym = (info.scores.result?.yakuman ?? 0) + (info.scores.result?.bonusYakuman ?? 0);
                s.TotalWinHan += ym > 0 ? ym * 13 : han;
                if (han > topHan) { topHan = han; topFu = fu; }
                if (ym > topYakuman) topYakuman = ym;

                foreach (var sc in info.scores) {
                  if (sc.Source != null &&
                      sc.Type is ScoringType.Han or ScoringType.Yakuman or ScoringType.BonusYakuman) {
                    var yName = sc.Source.GetType().Name;
                    s.YakuCounts[yName] = s.YakuCounts.GetValueOrDefault(yName) + 1;
                    if (!yakuNames.Contains(yName)) yakuNames.Add(yName);
                  }
                }
              }
            }

            pendingRound = new MatchRoundSummary {
              RoundIndex = RoundsPlayed,
              Label = currentRoundLabel,
              Outcome = isTsumo ? "tsumo" : "ron",
              WinnerSeats = winners,
              LoserSeat = !isTsumo && ValidSeat(fromSeat) ? fromSeat : null,
              Yaku = yakuNames,
              Han = topHan,
              Fu = topFu,
              Yakuman = topYakuman,
              UsedWildcard = anyWildcard,
            };
            Rounds.Add(pendingRound);
            break;
          }

          case EndGameRyuukyokuEvent draw: {
            FinalizeOpenHandsOnce();
            ExhaustiveDraws++;
            var tenpaiList = new List<int>();
            if (draw.tenpaiPlayers != null) {
              foreach (var seat in draw.tenpaiPlayers) {
                if (ValidSeat(seat)) {
                  stats[seat].TenpaiAtDrawCount++;
                  tenpaiList.Add(seat);
                }
              }
            }
            pendingRound = new MatchRoundSummary {
              RoundIndex = RoundsPlayed,
              Label = currentRoundLabel,
              Outcome = "exhaustive_draw",
              TenpaiSeats = tenpaiList,
            };
            Rounds.Add(pendingRound);
            break;
          }

          case MidGameRyuukyokuEvent abort: {
            FinalizeOpenHandsOnce();
            AbortedDraws++;
            pendingRound = new MatchRoundSummary {
              RoundIndex = RoundsPlayed,
              Label = currentRoundLabel,
              Outcome = "abortive_draw",
              AbortReason = abort.name,
            };
            Rounds.Add(pendingRound);
            break;
          }

          case ApplyScoreEvent apply when pendingRound != null: {
            var deltas = new List<int>(playerCount);
            for (int seat = 0; seat < playerCount; seat++) {
              int d = (int)apply.scoreChange.DeltaScore(seat);
              deltas.Add(d);
            }
            pendingRound.ScoreDeltas = deltas;

            if (pendingRound.Outcome is "tsumo" or "ron") {
              int wonTotal = 0;
              foreach (var w in pendingRound.WinnerSeats) {
                int gain = Math.Max(0, deltas[w]);
                wonTotal += gain;
                stats[w].TotalWinPoints += gain;
                if (gain > stats[w].MaxWinPoints) stats[w].MaxWinPoints = gain;
              }
              pendingRound.PointsWon = wonTotal;
              if (pendingRound.LoserSeat is int loser && ValidSeat(loser)) {
                stats[loser].DealInPoints += Math.Max(0, -deltas[loser]);
              }
            }
            pendingRound = null;
            break;
          }
        }
      }

      private void FinalizeOpenHandsOnce() {
        if (roundConcluded) return;
        roundConcluded = true;
        for (int s = 0; s < playerCount; s++) {
          if (openedInCurrentRound[s]) {
            stats[s].OpenHandCount++;
          }
        }
      }

      private bool ValidSeat(int seat) => seat >= 0 && seat < playerCount;
    }
  }
}
