using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RabiRiichi.Arena.Storage {
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum UsageErrorCategory {
    Network,
    Timeout,
    InvalidResponse,
    RateLimited,
    Auth,
    Other,
  }

  /// <summary>
  /// Per-model operational counters (requests, tokens, latency, errors) persisted to
  /// <c>{workspace}/stats.json</c>.
  /// </summary>
  public sealed class ModelUsage {
    public string ModelId { get; set; } = "";
    public long Requests { get; set; }
    public long Successes { get; set; }
    public long Failures { get; set; }

    public long NetworkErrors { get; set; }
    public long TimeoutErrors { get; set; }
    public long InvalidResponseErrors { get; set; }
    public long RateLimitedErrors { get; set; }
    public long AuthErrors { get; set; }
    public long OtherErrors { get; set; }

    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long TotalTokens { get; set; }
    public long TotalLatencyMs { get; set; }

    public long Retries { get; set; }
    public long Penalties { get; set; }

    public ModelUsage Copy() => new() {
      ModelId = ModelId,
      Requests = Requests,
      Successes = Successes,
      Failures = Failures,
      NetworkErrors = NetworkErrors,
      TimeoutErrors = TimeoutErrors,
      InvalidResponseErrors = InvalidResponseErrors,
      RateLimitedErrors = RateLimitedErrors,
      AuthErrors = AuthErrors,
      OtherErrors = OtherErrors,
      PromptTokens = PromptTokens,
      CompletionTokens = CompletionTokens,
      ReasoningTokens = ReasoningTokens,
      TotalTokens = TotalTokens,
      TotalLatencyMs = TotalLatencyMs,
      Retries = Retries,
      Penalties = Penalties,
    };

    internal void AddErrorLocked(UsageErrorCategory category) {
      switch (category) {
        case UsageErrorCategory.Network: NetworkErrors++; break;
        case UsageErrorCategory.Timeout: TimeoutErrors++; break;
        case UsageErrorCategory.InvalidResponse: InvalidResponseErrors++; break;
        case UsageErrorCategory.RateLimited: RateLimitedErrors++; break;
        case UsageErrorCategory.Auth: AuthErrors++; break;
        default: OtherErrors++; break;
      }
    }
  }

  /// <summary>
  /// Thread-safe per-model usage counters persisted to <c>{workspace}/stats.json</c>.
  /// </summary>
  public sealed class UsageStats {
    private static readonly JsonSerializerOptions JsonOptions = new() {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string path;
    private readonly object gate = new();
    private readonly Dictionary<string, ModelUsage> byModel = new();

    public UsageStats(string workspaceDir) {
      path = Path.Combine(workspaceDir, "stats.json");
      Load();
    }

    private void Load() {
      lock (gate) {
        byModel.Clear();
        if (!File.Exists(path)) {
          return;
        }
        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json)) {
          return;
        }
        var list = JsonSerializer.Deserialize<List<ModelUsage>>(json, JsonOptions)
            ?? new List<ModelUsage>();
        foreach (var u in list) {
          if (!string.IsNullOrEmpty(u.ModelId)) {
            byModel[u.ModelId] = u;
          }
        }
      }
    }

    private ModelUsage GetOrCreateLocked(string modelId) {
      if (!byModel.TryGetValue(modelId, out var u)) {
        u = new ModelUsage { ModelId = modelId };
        byModel[modelId] = u;
      }
      return u;
    }

    public void RecordRequest(string modelId) {
      Mutate(modelId, u => u.Requests++);
    }

    public void RecordSuccess(
        string modelId,
        long promptTokens = 0,
        long completionTokens = 0,
        long reasoningTokens = 0,
        long latencyMs = 0) {
      Mutate(modelId, u => {
        u.Successes++;
        u.PromptTokens += promptTokens;
        u.CompletionTokens += completionTokens;
        u.ReasoningTokens += reasoningTokens;
        u.TotalTokens += promptTokens + completionTokens;
        u.TotalLatencyMs += Math.Max(0, latencyMs);
      });
    }

    public void RecordFailure(string modelId, UsageErrorCategory category, long latencyMs = 0) {
      Mutate(modelId, u => {
        u.Failures++;
        u.TotalLatencyMs += Math.Max(0, latencyMs);
        u.AddErrorLocked(category);
      });
    }

    public void RecordRetry(string modelId, long count = 1) {
      Mutate(modelId, u => u.Retries += count);
    }

    public void RecordPenalty(string modelId, long count = 1) {
      Mutate(modelId, u => u.Penalties += count);
    }

    public void Reset(string modelId = null) {
      lock (gate) {
        if (string.IsNullOrWhiteSpace(modelId)) {
          byModel.Clear();
        } else {
          byModel.Remove(modelId);
        }
        SaveLocked();
      }
    }

    private void Mutate(string modelId, Action<ModelUsage> action) {
      if (string.IsNullOrEmpty(modelId)) {
        throw new ArgumentException("modelId must not be empty", nameof(modelId));
      }
      lock (gate) {
        action(GetOrCreateLocked(modelId));
        SaveLocked();
      }
    }

    public ModelUsage Get(string modelId) {
      lock (gate) {
        return byModel.TryGetValue(modelId, out var u) ? u.Copy() : null;
      }
    }

    public IReadOnlyList<ModelUsage> Snapshot() {
      lock (gate) {
        return byModel.Values.OrderBy(u => u.ModelId).Select(u => u.Copy()).ToList();
      }
    }

    private void SaveLocked() {
      var list = byModel.Values.OrderBy(u => u.ModelId).ToList();
      AtomicFile.WriteAllText(path, JsonSerializer.Serialize(list, JsonOptions));
    }
  }
}
