using System.Collections.Generic;
using System.Text.Json.Nodes;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Storage;

namespace RabiRiichi.Arena.Controllers {
  public sealed class ArenaMetadataDto {
    public IReadOnlyList<ArenaMetadata.ProviderPresetItem> Providers { get; set; } = [];
    public IReadOnlyList<ArenaMetadata.BaselineVariantItem> BaselineVariants { get; set; } = [];
    public IReadOnlyList<ArenaMetadata.TilePresetItem> TilePresets { get; set; } = [];
    public Dictionary<string, Dictionary<string, int>> PresetTileCounts { get; set; } = new();
    public IReadOnlyList<string> CanonicalTileCodes { get; set; } = [];
    public IReadOnlyList<ArenaMetadata.YakuCatalogItem> Yakus { get; set; } = [];
    public IReadOnlyList<string> DefaultAllowedYakus { get; set; } = [];
    public IReadOnlyList<ArenaMetadata.PolicyCategoryItem> PolicyCategories { get; set; } = [];
    public bool ExposeReasoningPublicly { get; set; }
    public string ClientUrl { get; set; } = "";
  }

  public sealed class RunSummaryDto {
    public string RunId { get; set; } = "";
    public string Label { get; set; } = "";
    public string ScheduleMode { get; set; } = "swiss";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string Status { get; set; } = "";
    public int SwissRounds { get; set; }
    public int CurrentSwissRound { get; set; }
    public int TargetMatches { get; set; }
    public int QueuedCustomTables { get; set; }
    public int CompletedMatches { get; set; }
    public int ModelCount { get; set; }
    public int PlayerCount { get; set; }
    public int TotalRound { get; set; }
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
    public bool Active { get; set; }
  }

  public sealed class LiveSeatDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long Points { get; set; }
    public bool InRiichi { get; set; }
  }

  public sealed class LiveMatchDto {
    public string GameId { get; set; } = "";
    public string RoundLabel { get; set; } = "";
    public int WallRemaining { get; set; }
    public int TurnCount { get; set; }
    public string StartedAt { get; set; } = "";
    public List<LiveSeatDto> Seats { get; set; } = new();
  }

  public sealed class NextMatchDto {
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
    public NextMatchPairingDto NextMatch { get; set; }
  }

  public sealed class NextMatchPairingDto {
    public int SwissRound { get; set; }
    public string Label { get; set; } = "";
    public bool Padded { get; set; }
    public List<NextMatchSeatDto> Seats { get; set; } = new();
  }

  public sealed class NextMatchSeatDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
  }

  public sealed class MatchListDto {
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<MatchListItemDto> Items { get; set; } = new();
  }

  public sealed class MatchListItemDto {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string BatchLabel { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public long DurationMs { get; set; }
    public int PlayerCount { get; set; }
    public int TotalRound { get; set; }
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
    public int RoundsPlayed { get; set; }
    public string ReplayLink { get; set; } = "";
    public List<string> SeatReplayLinks { get; set; } = new();
    public string ReplayDownloadUrl { get; set; } = "";
    public List<MatchListPlayerDto> Players { get; set; } = new();
  }

  public sealed class MatchListPlayerDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
    public int AgariCount { get; set; }
    public int DealInCount { get; set; }
    public int PenaltyCount { get; set; }
  }

  public sealed class MatchDetailDto {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string BatchLabel { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public long DurationMs { get; set; }
    public long Seed { get; set; }
    public int PlayerCount { get; set; }
    public int TotalRound { get; set; }
    public string TileSetPreset { get; set; } = "Regular";
    public bool HasWildcards { get; set; }
    public int WildcardCount { get; set; }
    public int RoundsPlayed { get; set; }
    public int ExhaustiveDraws { get; set; }
    public int AbortedDraws { get; set; }
    public JsonNode Config { get; set; }
    public string ReplayLink { get; set; } = "";
    public List<string> SeatReplayLinks { get; set; } = new();
    public string ReplayDownloadUrl { get; set; } = "";
    public bool HasReplayFile { get; set; }
    public List<int> ReasoningSeats { get; set; } = new();
    public List<MatchDetailPlayerDto> Players { get; set; } = new();
    public List<MatchRoundSummary> Rounds { get; set; } = new();
  }

  public sealed class MatchDetailPlayerDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloBefore { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }

    public int RoundsPlayed { get; set; }
    public int AgariCount { get; set; }
    public int TsumoCount { get; set; }
    public int RonCount { get; set; }
    public int DealInCount { get; set; }
    public int DealInPoints { get; set; }
    public int TotalWinPoints { get; set; }
    public int TotalWinHan { get; set; }
    public int MaxWinPoints { get; set; }
    public int RiichiCount { get; set; }
    public int OpenHandCount { get; set; }
    public int CallCount { get; set; }
    public int KanCount { get; set; }
    public int NukiCount { get; set; }
    public int TenpaiAtDrawCount { get; set; }
    public int WildcardWinCount { get; set; }
    public Dictionary<string, int> YakuCounts { get; set; } = new();

    public int DecisionsCount { get; set; }
    public int AutoDecisionsCount { get; set; }
    public int ValidFirstTryCount { get; set; }
    public int RetryCount { get; set; }
    public int PenaltyCount { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long ReasoningTokens { get; set; }
    public long TotalLatencyMs { get; set; }
    public long MaxLatencyMs { get; set; }
  }

  public sealed class ReasoningTrackDto {
    public string GameId { get; set; } = "";
    public List<ReasoningTrackEntryDto> Entries { get; set; } = new();
  }

  public sealed class ReasoningTrackEntryDto {
    public int EventIndex { get; set; }
    public int Seat { get; set; }
    public string DisplayName { get; set; } = "";
    public string Rationale { get; set; } = "";
    public bool Valid { get; set; }
  }
}
