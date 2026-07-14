using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// The outcome of hypothetically discarding a tile: the resulting shanten
  /// (0 = tenpai, -1 = complete) and the ukeire (how many live tiles advance the
  /// hand, counting only copies the viewer has not yet seen).
  /// </summary>
  public readonly struct DiscardEval(GameTile discard, int shanten, int ukeire) {
    public GameTile Discard { get; } = discard;
    public int Shanten { get; } = shanten;
    public int Ukeire { get; } = ukeire;
  }

  /// <summary>
  /// A read-only, per-seat view over the authoritative <see cref="Game"/> that
  /// exposes only information a fair player at that seat could legitimately know.
  ///
  /// The engine hands agents the full <see cref="Game"/> object, which also
  /// contains hidden state (opponents' concealed tiles, the wall order, unrevealed
  /// dora/uradora indicators). A non-cheating AI must restrict itself to public
  /// information; this wrapper makes that restriction explicit and enforceable so
  /// the AI physically cannot read hidden fields.
  ///
  /// Publicly known information (see server investigation notes):
  ///  - the viewer's own hand (free tiles, pending draw, called melds);
  ///  - every player's discards and called melds;
  ///  - revealed dora indicators only;
  ///  - the drawable wall count (the number, not the tiles);
  ///  - riichi status, jun, points, seat winds, round/dealer/honba.
  /// </summary>
  public sealed class PublicGameView {
    private readonly Game game;

    /// <summary> The seat this view belongs to. </summary>
    public int Seat { get; }

    public PublicGameView(Game game, int seat) {
      this.game = game;
      Seat = seat;
    }

    /// <summary> The viewer's own player. Reading its hand is fair. </summary>
    public Player Self => game.GetPlayer(Seat);

    /// <summary> The viewer's own hand (private to the viewer, so fair to read). </summary>
    public Hand SelfHand => Self.hand;

    /// <summary> Whether the viewer's hand is still fully concealed (no calls). </summary>
    public bool SelfMenzen => SelfHand.menzen;

    /// <summary> Whether the viewer has declared riichi. </summary>
    public bool SelfRiichi => SelfHand.riichi;

    /// <summary> The viewer's own called (open) melds. </summary>
    public IReadOnlyList<MenLike> SelfCalled => SelfHand.called;

    public int PlayerCount => game.config.playerCount;

    #region Round / seat context (all public)
    public Wind RoundWind => game.info.wind;
    public Wind SelfWind => Self.Wind;
    public int Round => game.info.round;
    public int Dealer => game.info.dealer;
    public int Honba => game.info.honba;
    public int RiichiStick => game.info.riichiStick;
    public bool IsAllLast => game.info.IsAllLast;
    public bool SelfIsDealer => Self.IsDealer;
    #endregion

    #region Wall (only the public count and revealed dora indicators)
    /// <summary> Number of drawable tiles left in the wall (a public count). </summary>
    public int WallRemaining => game.wall.NumRemaining;

    /// <summary> The revealed dora indicators (never the hidden ones or uradora). </summary>
    public IReadOnlyList<Tile> RevealedDoraIndicators =>
        game.wall.doras.Take(game.wall.revealedDoraCount).Select(t => t.tile).ToList();

    /// <summary> How many dora <paramref name="tile"/> is worth from revealed indicators. </summary>
    public int CountDora(Tile tile) => game.wall.CountDora(tile);
    #endregion

    #region Per-opponent public info
    public IEnumerable<int> AllSeats => Enumerable.Range(0, PlayerCount);
    public IEnumerable<int> OpponentSeats => AllSeats.Where(s => s != Seat);

    private Player PlayerAt(int seat) => game.GetPlayer(seat);

    /// <summary> A player's discard river (public). </summary>
    public IReadOnlyList<GameTile> DiscardsOf(int seat) => PlayerAt(seat).hand.discarded;

    /// <summary> A player's called (open) melds (public). </summary>
    public IReadOnlyList<MenLike> CalledOf(int seat) => PlayerAt(seat).hand.called;

    public bool IsRiichi(int seat) => PlayerAt(seat).hand.riichi;
    public bool IsIppatsu(int seat) => PlayerAt(seat).hand.ippatsu;
    public int JunOf(int seat) => PlayerAt(seat).hand.jun;
    public long PointsOf(int seat) => PlayerAt(seat).points;
    public Wind WindOf(int seat) => PlayerAt(seat).Wind;
    public bool IsDealer(int seat) => PlayerAt(seat).IsDealer;
    #endregion

    /// <summary>
    /// Counts how many copies of a tile kind (akadora-normalized) the viewer can
    /// currently see: own free + pending tiles, everyone's melds and discards, and
    /// the revealed dora indicators. Never inspects hidden hands or the wall.
    /// </summary>
    public int VisibleCount(Tile kind) {
      var target = kind.WithoutDora;
      int count = 0;

      foreach (var tile in SelfHand.freeTiles) {
        if (tile.tile.WithoutDora == target)
          count++;
      }
      if (SelfHand.pendingTile != null && SelfHand.pendingTile.tile.WithoutDora == target) {
        count++;
      }
      foreach (var seat in AllSeats) {
        var hand = PlayerAt(seat).hand;
        foreach (var meld in hand.called) {
          foreach (var tile in meld) {
            if (tile.tile.WithoutDora == target)
              count++;
          }
        }
        foreach (var tile in hand.discarded) {
          if (tile.tile.WithoutDora == target)
            count++;
        }
      }
      foreach (var indicator in RevealedDoraIndicators) {
        if (indicator.WithoutDora == target)
          count++;
      }
      return count;
    }

    /// <summary>
    /// How many copies of <paramref name="kind"/> could still be live (unseen by
    /// the viewer), out of the four in a standard set.
    /// </summary>
    public int UnseenCount(Tile kind) => System.Math.Max(0, 4 - VisibleCount(kind));

    /// <summary>
    /// Evaluates hypothetically discarding <paramref name="discard"/> from the
    /// given 14-tile holding: the resulting shanten and the ukeire (live
    /// acceptance) toward a win.
    ///
    /// The computation runs on a throwaway <see cref="Hand"/> built from the
    /// viewer's OWN tiles, so it never mutates live game state and never reads any
    /// opponent's concealed tiles. Acceptance is weighted by
    /// <see cref="UnseenCount(Tile)"/>, which itself only uses public info.
    /// </summary>
    public DiscardEval EvaluateDiscard(GameTile discard, IReadOnlyList<GameTile> hand14) {
      // A temporary hand bound to our own player (so hand.game resolves), holding
      // the tiles left after the discard, plus our real called melds.
      var remaining = hand14.Where(t => !ReferenceEquals(t, discard)).ToList();
      var tempHand = new Hand {
        player = Self,
        freeTiles = remaining,
        called = SelfHand.called,
      };

      // The resolver's acceptance computation (incoming == null) requires exactly
      // a 13-tile hand (melds count as 3 each). If the caller passed an
      // unexpected count, report an unreachable shanten rather than throwing.
      if (tempHand.Count != Game.HAND_SIZE) {
        return new DiscardEval(discard, int.MaxValue, 0);
      }

      var resolver = game.Get<PatternResolver>();
      int shanten = resolver.ResolveShanten(tempHand, null, out var acceptance);
      int ukeire = 0;
      if (acceptance != null) {
        foreach (var tile in acceptance) {
          ukeire += UnseenCount(tile);
        }
      }
      return new DiscardEval(discard, shanten, ukeire);
    }

    /// <summary>
    /// Best (lowest) shanten of a hypothetical concealed holding, built from the
    /// viewer's OWN tiles plus their existing called melds. The holding must be a
    /// legal size (13 or 14 counting melds as 3 each); otherwise an unreachable
    /// shanten is reported. Runs on a throwaway <see cref="Hand"/>, so it never
    /// mutates game state or reads hidden tiles.
    /// </summary>
    public int ShantenOf(IReadOnlyList<GameTile> freeTiles) {
      return ShantenOf(freeTiles, SelfHand.called);
    }

    /// <summary>
    /// Shanten of a hypothetical holding with an explicit meld set. Used to score
    /// calls (chii/pon/kan) that both add a meld and consume free tiles.
    /// </summary>
    public int ShantenOf(IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called) {
      var tempHand = new Hand {
        player = Self,
        freeTiles = [.. freeTiles],
        called = [.. called],
      };
      // The resolver expects a 13-tile (waiting) or 14-tile (with incoming) hand,
      // melds counting as 3. For a post-call hand we evaluate the 13-tile wait.
      if (tempHand.Count is not Game.HAND_SIZE and not (Game.HAND_SIZE + 1)) {
        return int.MaxValue;
      }
      var resolver = game.Get<PatternResolver>();
      return resolver.ResolveShanten(tempHand, null, out _, Game.HAND_SIZE);
    }
  }
}
