using System.Collections.Generic;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Controllers {
  public sealed class AdminLoginRequestDto {
    public string Password { get; set; } = "";
  }

  public sealed class AdminSessionDto {
    public bool Authenticated { get; set; }
    public string CsrfToken { get; set; } = "";
    public bool DefaultPasswordWarning { get; set; }
  }

  public sealed class AdminConfigSaveResultDto {
    public bool Saved { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> RestartRequiredFields { get; set; } = new();
  }

  public sealed class StartRunRequestDto {
    public string Label { get; set; } = "";
    public string ScheduleMode { get; set; }
    public int? SwissRounds { get; set; }
    public int? TargetMatches { get; set; }
    public int? MatchConcurrency { get; set; }
    public List<List<string>> CustomTables { get; set; }
  }

  public sealed class AppendBatchRequestDto {
    /// <summary> "swiss_rounds" | "balanced_matches" | "custom_lineup". </summary>
    public string Mode { get; set; } = "balanced_matches";
    public int Count { get; set; } = 1;
    public string Label { get; set; } = "";
    public List<string> SeatModelIds { get; set; } = new();
  }

  public sealed class ModelTestResultDto {
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public long LatencyMs { get; set; }
  }

  public sealed class AdminStatusDto {
    public AdminRunStateDto Run { get; set; } = new();
    public List<RunSummaryDto> Runs { get; set; } = new();
    public List<AdminModelUsageDto> Models { get; set; } = new();
  }

  public sealed class AdminRunStateDto {
    public string Status { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Label { get; set; } = "";
    public string ScheduleMode { get; set; } = "swiss";
    public int CurrentSwissRound { get; set; }
    public int TotalSwissRounds { get; set; }
    public int CompletedTablesInRound { get; set; }
    public int TotalTablesInRound { get; set; }
    public int TargetMatches { get; set; }
    public int QueuedCustomTables { get; set; }
    public int CompletedMatches { get; set; }
    public int MatchConcurrency { get; set; }
    public string LastError { get; set; }
    public List<LiveMatchDto> ActiveMatches { get; set; } = new();
  }

  public sealed class AdminModelUsageDto {
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
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
    public double AvgLatencyMs { get; set; }

    public long Retries { get; set; }
    public long Penalties { get; set; }
  }

  public sealed class AdminReasoningDto {
    public ReasoningMeta Meta { get; set; }
    public IReadOnlyList<ReasoningTurn> Turns { get; set; } = new List<ReasoningTurn>();
  }
}
