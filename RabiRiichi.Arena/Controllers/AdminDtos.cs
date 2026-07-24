using System.Collections.Generic;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// DTOs returned/accepted by <see cref="AdminController"/> (ARENA_DESIGN.md
  /// §12b/§12c). Secrets NEVER appear in outbound DTOs: the config editor sends a
  /// <see cref="ArenaConfig"/> already redacted by <c>Redacted()</c>, and status
  /// projects only usage counters + run state (no apiKeys).
  /// </summary>

  /// <summary>Result of a config PUT: validation errors + restart-required notes.</summary>
  public sealed class AdminConfigSaveResultDto {
    public bool Saved { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>Fields that require a process restart to take effect (§12b).</summary>
    public List<string> RestartRequiredFields { get; set; } = new();
  }

  /// <summary>Per-model monitoring counters + current run/match state (§12c).</summary>
  public sealed class AdminStatusDto {
    public AdminRunStateDto Run { get; set; } = new();

    /// <summary>All runs (newest-first) for the admin runs list (multi-run).</summary>
    public List<RunSummaryDto> Runs { get; set; } = new();

    public List<AdminModelUsageDto> Models { get; set; } = new();
  }

  /// <summary>Current run/match state projected from <c>ArenaService.GetStatus()</c>.</summary>
  public sealed class AdminRunStateDto {
    public string Status { get; set; } = "";
    public string RunId { get; set; } = "";
    public int CurrentSwissRound { get; set; }
    public int TotalSwissRounds { get; set; }
    public int CompletedTablesInRound { get; set; }
    public int TotalTablesInRound { get; set; }
    public int CompletedMatches { get; set; }
    public double? SecondsToNextMatch { get; set; }
  }

  /// <summary>
  /// One model's usage counters for the monitoring dashboard (§12c). Mirrors
  /// <see cref="ModelUsage"/> but is a clean DTO that provably carries no secret.
  /// </summary>
  public sealed class AdminModelUsageDto {
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Requests { get; set; }
    public long Successes { get; set; }
    public long Failures { get; set; }

    // Failure breakdown by category (§12c).
    public long NetworkErrors { get; set; }
    public long TimeoutErrors { get; set; }
    public long InvalidResponseErrors { get; set; }
    public long RateLimitedErrors { get; set; }
    public long AuthErrors { get; set; }
    public long OtherErrors { get; set; }

    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }

    public long Retries { get; set; }
    public long Penalties { get; set; }
  }

  /// <summary>A reasoning transcript (meta + turns) for admin inspection (§8/§12b).</summary>
  public sealed class AdminReasoningDto {
    public ReasoningMeta Meta { get; set; }
    public IReadOnlyList<ReasoningTurn> Turns { get; set; } = new List<ReasoningTurn>();
  }
}
