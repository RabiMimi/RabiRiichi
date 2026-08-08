using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// Per-model rating record persisted in <c>ratings.json</c>. Placement counts
  /// are 1st/2nd/3rd/4th. See ARENA_DESIGN.md §11a.
  /// </summary>
  public sealed class RatingRecord {
    public string ModelId { get; set; } = "";
    public double Elo { get; set; }
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Place1 { get; set; }
    public int Place2 { get; set; }
    public int Place3 { get; set; }
    public int Place4 { get; set; }
    public int Penalties { get; set; }
  }

  /// <summary>
  /// Loads/saves per-model Elo + counters to <c>{workspace}/ratings.json</c>.
  /// Load on boot, cache in memory, write-through on change (§11). All writes
  /// are atomic and guarded by a lock.
  /// </summary>
  public sealed class RatingStore {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string path;
    private readonly object gate = new();
    private readonly Dictionary<string, RatingRecord> ratings = new();

    public RatingStore(string workspaceDir) {
      path = Path.Combine(workspaceDir, "ratings.json");
      Load();
    }

    private void Load() {
      lock (gate) {
        ratings.Clear();
        if (!File.Exists(path)) {
          return;
        }
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) {
          return;
        }
        var list = JsonSerializer.Deserialize<List<RatingRecord>>(json, JsonOptions)
            ?? new List<RatingRecord>();
        foreach (var r in list) {
          if (!string.IsNullOrEmpty(r.ModelId)) {
            ratings[r.ModelId] = r;
          }
        }
      }
    }

    /// <summary>Returns the record for a model, or null if none exists yet.</summary>
    public RatingRecord Get(string modelId) {
      lock (gate) {
        return ratings.TryGetValue(modelId, out var r) ? Copy(r) : null;
      }
    }

    /// <summary>Snapshot of all rating records.</summary>
    public IReadOnlyList<RatingRecord> GetAll() {
      lock (gate) {
        return ratings.Values.Select(Copy).ToList();
      }
    }

    /// <summary>
    /// Inserts or replaces the record for <c>record.ModelId</c> and persists.
    /// </summary>
    public void Update(RatingRecord record) {
      if (record == null || string.IsNullOrEmpty(record.ModelId)) {
        throw new ArgumentException("record.ModelId must not be empty", nameof(record));
      }
      lock (gate) {
        ratings[record.ModelId] = Copy(record);
        SaveLocked();
      }
    }

    /// <summary>Persists the current in-memory ratings atomically.</summary>
    public void Save() {
      lock (gate) {
        SaveLocked();
      }
    }

    private void SaveLocked() {
      var list = ratings.Values.OrderBy(r => r.ModelId).ToList();
      AtomicFile.WriteAllText(path, JsonSerializer.Serialize(list, JsonOptions));
    }

    private static RatingRecord Copy(RatingRecord r) => new() {
      ModelId = r.ModelId,
      Elo = r.Elo,
      Games = r.Games,
      Wins = r.Wins,
      Place1 = r.Place1,
      Place2 = r.Place2,
      Place3 = r.Place3,
      Place4 = r.Place4,
      Penalties = r.Penalties,
    };
  }
}
