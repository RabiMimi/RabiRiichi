using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Arena.Storage {
  /// <summary>
  /// One rationale pinned to a replay position. <see cref="EventIndex"/> indexes
  /// the event-only stream a replay viewer reconstructs from the game log, so a
  /// viewer can surface the line exactly when the move it explains happens.
  /// </summary>
  public sealed class ReasoningTrackEntry {
    public int EventIndex { get; set; }
    public int Seat { get; set; }
    public string DisplayName { get; set; } = "";
    public string Rationale { get; set; } = "";

    /// <summary>False when the model failed and the safe default was used.</summary>
    public bool Valid { get; set; }
  }

  /// <summary>
  /// Projects the on-disk reasoning transcripts of one match into a compact,
  /// replay-aligned track (ARENA_DESIGN.md §9).
  ///
  /// §9 promised the rationale would ride along "in the replay", but it cannot:
  /// <c>GameLogMsg</c>/<c>SingleLogMsg</c> only carry events and inquiries, and
  /// <c>Room.BroadcastChat</c> fans chat out to connected humans without ever
  /// touching the replay log — so in a headless Arena match (no humans) the
  /// rationale reached nobody. This track is the replacement: the same data,
  /// served beside the replay and anchored to it by event index, with no wire
  /// format change.
  ///
  /// Only the SHORT rationale is projected. The full prompt/response transcript
  /// stays admin-only (§8/§12b).
  /// </summary>
  public static class ReasoningTrack {
    /// <summary>
    /// Builds the ordered track for <paramref name="gameId"/> across
    /// <paramref name="seats"/>. Turns with no replay anchor (written before the
    /// anchor existed, or recorded with no replay being captured) and turns with
    /// an empty rationale are skipped — they have nothing to show.
    /// </summary>
    /// <param name="displayNameOf">Seat → model display name; may be null.</param>
    public static IReadOnlyList<ReasoningTrackEntry> Build(
        ReasoningStore store,
        string gameId,
        IEnumerable<int> seats,
        Func<int, string> displayNameOf = null) {
      var entries = new List<ReasoningTrackEntry>();
      if (store == null || !ReasoningStore.IsValidGameId(gameId) || seats == null) {
        return entries;
      }
      foreach (var seat in seats) {
        if (!ReasoningStore.IsValidSeat(seat)) {
          continue;
        }
        foreach (var turn in store.ReadTurns(gameId, seat)) {
          if (turn.EventIndex < 0 || string.IsNullOrWhiteSpace(turn.Rationale)) {
            continue;
          }
          entries.Add(new ReasoningTrackEntry {
            EventIndex = turn.EventIndex,
            Seat = seat,
            DisplayName = displayNameOf?.Invoke(seat) ?? "",
            Rationale = turn.Rationale,
            Valid = turn.Valid,
          });
        }
      }
      // Replay order first; seat order only to keep simultaneous decisions
      // (a discard and the reaction inquiries it triggers) deterministic.
      return entries
          .OrderBy(e => e.EventIndex)
          .ThenBy(e => e.Seat)
          .ToList();
    }
  }
}
