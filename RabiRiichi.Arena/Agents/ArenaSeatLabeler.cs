using System;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Resolves the human-facing label an Arena agent may use for a seat, honoring
  /// the opponent-anonymity exposure gate (ARENA_DESIGN.md §9a).
  ///
  /// When <see cref="RevealIdentity"/> is false (the default), every seat other
  /// than the viewer's own is presented ONLY by a neutral seat label (its wind,
  /// e.g. "East", plus "Player N"); the agent never receives the opponent's
  /// display name, model id, provider, variant, or the fact that a seat is a
  /// baseline vs. an LLM. When true, real display names may be surfaced.
  ///
  /// <see cref="PublicGameView"/> already keys all opponent data by seat (which
  /// is identity-neutral), so anonymity here is purely about masking the
  /// human-facing label; no numeric/seat tool data leaks identity.
  /// </summary>
  public sealed class ArenaSeatLabeler {
    private readonly bool revealIdentity;
    private readonly Func<int, string> displayNameOf;
    private readonly int selfSeat;
    private readonly Func<int, Wind> windOf;

    /// <param name="revealIdentity">The <c>exposure.revealOpponentIdentity</c> gate.</param>
    /// <param name="selfSeat">The viewer's own seat (always labeled "You").</param>
    /// <param name="windOf">Maps a seat to its seat wind (used for neutral labels).</param>
    /// <param name="displayNameOf">
    /// Resolves a seat's real display name; only consulted when identities are
    /// revealed. May be null when anonymized.
    /// </param>
    public ArenaSeatLabeler(
        bool revealIdentity,
        int selfSeat,
        Func<int, Wind> windOf,
        Func<int, string> displayNameOf = null) {
      this.revealIdentity = revealIdentity;
      this.selfSeat = selfSeat;
      this.windOf = windOf;
      this.displayNameOf = displayNameOf;
    }

    /// <summary>Whether real opponent identities may be surfaced to the agent.</summary>
    public bool RevealIdentity => revealIdentity;

    /// <summary>
    /// A stable, always-identity-neutral label for a seat: its seat wind and
    /// numeric player index (e.g. "East (Player 1)"). Used even when identity is
    /// revealed as the fallback if no display name is available.
    /// </summary>
    public string NeutralLabel(int seat) {
      var wind = WindName(windOf(seat));
      return $"{wind} (Player {seat + 1})";
    }

    /// <summary>
    /// The label to print for a seat in prompts/menu/tool-context. Own seat is
    /// "You". Opponents are neutral seat labels under anonymity; when identity is
    /// revealed and a non-empty display name exists, it is appended.
    /// </summary>
    public string Label(int seat) {
      if (seat == selfSeat) {
        return "You";
      }
      var neutral = NeutralLabel(seat);
      if (!revealIdentity) {
        return neutral;
      }
      var name = displayNameOf?.Invoke(seat);
      return string.IsNullOrWhiteSpace(name) ? neutral : $"{name} [{neutral}]";
    }

    /// <summary>
    /// The label to attribute an incoming chat line to. Mirrors <see cref="Label"/>
    /// so a sender is never revealed by name under anonymity (§9).
    /// </summary>
    public string ChatSenderLabel(int seat) => Label(seat);

    private static string WindName(Wind wind) => wind switch {
      Wind.E => "East",
      Wind.S => "South",
      Wind.W => "West",
      Wind.N => "North",
      _ => wind.ToString(),
    };
  }
}
