using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Written-once per (gameId, seat) header for a reasoning transcript. The
  /// system prompt / static preamble lives here so per-turn lines stay
  /// de-duplicated. See ARENA_DESIGN.md §8.
  /// </summary>
  public sealed class ReasoningMeta {
    public string GameId { get; set; } = "";
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string CreatedAt { get; set; } = "";
  }

  /// <summary>
  /// One append-only line in <c>turns.jsonl</c> — the decision artifact for a
  /// single turn. <see cref="PromptDelta"/> is ONLY the new user message(s) this
  /// turn (never the accumulated history), so the full transcript for turn N is
  /// meta.systemPrompt + promptDelta of turns 1..N. See §8.
  /// </summary>
  public sealed class ReasoningTurn {
    public int TurnSeq { get; set; }
    public string PromptDelta { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string RawResponse { get; set; } = "";
    public string ParsedAction { get; set; } = "";
    public string Rationale { get; set; } = "";
    public bool Valid { get; set; }
    public int Attempts { get; set; }
    public bool Penalized { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long LatencyMs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Error { get; set; }

    public string Timestamp { get; set; } = "";
  }

  /// <summary>
  /// Persists per-decision thinking artifacts under
  /// <c>{workspace}/reasoning/{gameId}/seat{n}/</c>: a written-once
  /// <c>meta.json</c> and an append-only <c>turns.jsonl</c> (one JSON line per
  /// decision). Ids are validated for path safety, mirroring
  /// <c>ReplayStore.IsValidGameId</c>. Retention: keep forever (§8).
  /// </summary>
  public sealed class ReasoningStore {
    private static readonly JsonSerializerOptions MetaOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Compact (single-line) options for JSONL entries.
    private static readonly JsonSerializerOptions TurnOptions = new() {
      WriteIndented = false,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Mirror ReplayStore.GameIdRegex for path safety.
    private static readonly Regex GameIdRegex = new(@"^[0-9A-Za-z-]+$");

    private readonly string reasoningDir;
    private readonly object gate = new();

    public ReasoningStore(string workspaceDir) {
      reasoningDir = Path.Combine(workspaceDir, "reasoning");
    }

    /// <summary>True for gameIds that are safe to embed in a path.</summary>
    public static bool IsValidGameId(string gameId) =>
        !string.IsNullOrEmpty(gameId) && GameIdRegex.IsMatch(gameId);

    /// <summary>True for non-negative seat indices.</summary>
    public static bool IsValidSeat(int seat) => seat >= 0;

    private string SeatDir(string gameId, int seat) {
      if (!IsValidGameId(gameId)) {
        throw new ArgumentException("Invalid game ID", nameof(gameId));
      }
      if (!IsValidSeat(seat)) {
        throw new ArgumentException("Invalid seat", nameof(seat));
      }
      return Path.Combine(reasoningDir, gameId, $"seat{seat}");
    }

    /// <summary>
    /// Writes <paramref name="meta"/> exactly once. If a meta file already
    /// exists for this (gameId, seat), the call is a no-op and returns false;
    /// otherwise it is written atomically and returns true.
    /// </summary>
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

    /// <summary>Reads the meta for a (gameId, seat), or null if none.</summary>
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

    /// <summary>Appends one decision line to <c>turns.jsonl</c>.</summary>
    public void AppendTurn(string gameId, int seat, ReasoningTurn turn) {
      if (turn == null) {
        throw new ArgumentNullException(nameof(turn));
      }
      var dir = SeatDir(gameId, seat);
      var turnsPath = Path.Combine(dir, "turns.jsonl");
      var line = JsonSerializer.Serialize(turn, TurnOptions) + "\n";
      lock (gate) {
        Directory.CreateDirectory(dir);
        // Append is naturally O(1); a single writer + this lock keeps lines intact.
        File.AppendAllText(turnsPath, line);
      }
    }

    /// <summary>
    /// Reads all decision lines for a (gameId, seat) in append order. Returns an
    /// empty list if the file does not exist. Blank lines are skipped.
    /// </summary>
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
