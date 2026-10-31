using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Written-once per (gameId, seat) header for a reasoning transcript.
  /// </summary>
  public sealed class ReasoningMeta {
    public string GameId { get; set; } = "";
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string ThinkingLevel { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string CreatedAt { get; set; } = "";
  }

  /// <summary>
  /// One append-only decision entry in <c>turns.jsonl</c>.
  /// </summary>
  public sealed class ReasoningTurn {
    public int TurnSeq { get; set; }
    public int EventIndex { get; set; } = -1;
    public string RoundLabel { get; set; } = "";
    public string PromptDelta { get; set; } = "";
    public string ThinkingText { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string RawResponse { get; set; } = "";
    public string ParsedAction { get; set; } = "";
    public string Rationale { get; set; } = "";
    public string TableChat { get; set; }
    public bool Valid { get; set; }
    public int Attempts { get; set; }
    public bool Penalized { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long LatencyMs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Error { get; set; }

    public string Timestamp { get; set; } = "";
  }

  /// <summary>
  /// Persists per-decision thinking artifacts under
  /// <c>{workspace}/reasoning/{gameId}/seat{n}/</c>.
  /// </summary>
  public sealed class ReasoningStore {
    private static readonly JsonSerializerOptions MetaOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions TurnOptions = new() {
      WriteIndented = false,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex GameIdRegex = new(@"^[0-9A-Za-z-]+$", RegexOptions.Compiled);

    private readonly string reasoningDir;
    private readonly object gate = new();

    public ReasoningStore(string workspaceDir) {
      reasoningDir = Path.Combine(workspaceDir, "reasoning");
    }

    public static bool IsValidGameId(string gameId) =>
        !string.IsNullOrEmpty(gameId) && GameIdRegex.IsMatch(gameId);

    public static bool IsValidSeat(int seat) => seat is >= 0 and <= 7;

    private string SeatDir(string gameId, int seat) {
      if (!IsValidGameId(gameId)) {
        throw new ArgumentException("Invalid game ID", nameof(gameId));
      }
      if (!IsValidSeat(seat)) {
        throw new ArgumentException("Invalid seat", nameof(seat));
      }
      return Path.Combine(reasoningDir, gameId, $"seat{seat}");
    }

    public bool WriteMeta(ReasoningMeta meta) {
      if (meta == null) {
        throw new ArgumentNullException(nameof(meta));
      }
      var dir = SeatDir(meta.GameId, meta.Seat);
      var metaPath = Path.Combine(dir, "meta.json");
      lock (gate) {
        if (File.Exists(metaPath)) {
          return false;
        }
        Directory.CreateDirectory(dir);
        AtomicFile.WriteAllText(metaPath, JsonSerializer.Serialize(meta, MetaOptions));
        return true;
      }
    }

    public ReasoningMeta ReadMeta(string gameId, int seat) {
      var metaPath = Path.Combine(SeatDir(gameId, seat), "meta.json");
      lock (gate) {
        if (!File.Exists(metaPath)) {
          return null;
        }
        return JsonSerializer.Deserialize<ReasoningMeta>(
            File.ReadAllText(metaPath), MetaOptions);
      }
    }

    public void AppendTurn(string gameId, int seat, ReasoningTurn turn) {
      if (turn == null) {
        throw new ArgumentNullException(nameof(turn));
      }
      var dir = SeatDir(gameId, seat);
      var turnsPath = Path.Combine(dir, "turns.jsonl");
      var line = JsonSerializer.Serialize(turn, TurnOptions) + "\n";
      lock (gate) {
        Directory.CreateDirectory(dir);
        File.AppendAllText(turnsPath, line);
      }
    }

    public IReadOnlyList<ReasoningTurn> ReadTurns(string gameId, int seat) {
      var turnsPath = Path.Combine(SeatDir(gameId, seat), "turns.jsonl");
      lock (gate) {
        var result = new List<ReasoningTurn>();
        if (!File.Exists(turnsPath)) {
          return result;
        }
        foreach (var line in File.ReadAllLines(turnsPath)) {
          if (string.IsNullOrWhiteSpace(line)) {
            continue;
          }
          var turn = JsonSerializer.Deserialize<ReasoningTurn>(line, TurnOptions);
          if (turn != null) {
            result.Add(turn);
          }
        }
        return result;
      }
    }
  }
}
