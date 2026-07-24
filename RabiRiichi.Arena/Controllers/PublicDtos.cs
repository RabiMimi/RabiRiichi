using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// Clean JSON DTOs returned by <see cref="PublicController"/> (ARENA_DESIGN.md
  /// §12a). These deliberately expose ONLY human-facing, non-secret fields:
  /// display names, Elo, placements, scores, timestamps, replay links, and the
  /// finished-game config + real seed. No API keys, admin password, provider
  /// internals, or live-game seeds ever appear here.
  /// </summary>
  /// <summary>
  /// One run in the run selector / runs list (§12a, multi-run). Display-only; no
  /// secrets. <see cref="Active"/> marks the run the scheduler is bound to.
  /// </summary>
  public sealed class RunSummaryDto {
    public string RunId { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string Status { get; set; } = "";
    public int SwissRounds { get; set; }
    public int CurrentSwissRound { get; set; }
    public int CompletedMatches { get; set; }
    public int ModelCount { get; set; }
    public bool Active { get; set; }
  }

  public sealed class LeaderboardEntryDto {
    public int Rank { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double Elo { get; set; }
    public int Games { get; set; }
    public int Wins { get; set; }
    public double AvgPlacement { get; set; }
    public int Place1 { get; set; }
    public int Place2 { get; set; }
    public int Place3 { get; set; }
    public int Place4 { get; set; }
    public int Penalties { get; set; }
  }

  /// <summary>Run status + next-match preview for the public header (§12a).</summary>
  public sealed class NextMatchDto {
    public string Status { get; set; } = "";
    public string RunId { get; set; } = "";
    public int CurrentSwissRound { get; set; }
    public int TotalSwissRounds { get; set; }
    public int CompletedTablesInRound { get; set; }
    public int TotalTablesInRound { get; set; }
    public int CompletedMatches { get; set; }
    public double? SecondsToNextMatch { get; set; }

    /// <summary>The upcoming pairing, or null when none is pending.</summary>
    public NextMatchPairingDto NextMatch { get; set; }
  }

  public sealed class NextMatchPairingDto {
    public int SwissRound { get; set; }
    public bool Padded { get; set; }
    public List<NextMatchSeatDto> Seats { get; set; } = new();
  }

  public sealed class NextMatchSeatDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
  }

  /// <summary>A page of match summaries (newest-first) for the public list (§12a).</summary>
  public sealed class MatchListDto {
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<MatchListItemDto> Items { get; set; } = new();
  }

  public sealed class MatchListItemDto {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string FinishedAt { get; set; } = "";
    public string ReplayLink { get; set; } = "";
    public List<MatchListPlayerDto> Players { get; set; } = new();
  }

  public sealed class MatchListPlayerDto {
    public string DisplayName { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
  }

  /// <summary>Full match detail incl. per-game config + real seed (§12a).</summary>
  public sealed class MatchDetailDto {
    public string MatchId { get; set; } = "";
    public string GameId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SwissRound { get; set; }
    public string StartedAt { get; set; } = "";
    public string FinishedAt { get; set; } = "";

    /// <summary>The real, post-game seed (finished games only). See §11b.</summary>
    public long Seed { get; set; }

    /// <summary>Full GameConfig snapshot (same fields as the client info modal).</summary>
    public JsonNode Config { get; set; }

    public string ReplayLink { get; set; } = "";
    public List<MatchDetailPlayerDto> Players { get; set; } = new();
  }

  public sealed class MatchDetailPlayerDto {
    public int Seat { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int FinalPoints { get; set; }
    public int Placement { get; set; }
    public int PenaltyCount { get; set; }
    public double EloBefore { get; set; }
    public double EloAfter { get; set; }
    public double EloDelta { get; set; }
  }
}
