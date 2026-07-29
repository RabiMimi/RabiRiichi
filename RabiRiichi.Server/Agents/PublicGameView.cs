using RabiRiichi.Core;
using RabiRiichi.Actions;
using RabiRiichi.Patterns;
using RabiRiichi.Core.Config;

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

    #region Game configuration (public, immutable during a game)
    public int TotalRound => game.config.totalRound;
    public int MinHan => game.config.minHan;
    public int InitialTileCount => game.config.initialTiles?.Count ?? 0;
    public IReadOnlyList<Tile> InitialTiles => game.config.initialTiles ?? [];
    public long InitialPoints => game.config.pointThreshold.initialPoints;
    public long FinishPoints => game.config.pointThreshold.finishPoints;
    public long RiichiPoints => game.config.pointThreshold.riichiPoints;
    public long HonbaPoints => game.config.pointThreshold.honbaPoints;
    public double GameplayActionTimeout => game.config.gameplayActionTimeout;
    public string RenchanPolicy => game.config.renchanPolicy.ToString();
    public string EndGamePolicy => game.config.endGamePolicy.ToString();
    public string KuikaePolicy => game.config.kuikaePolicy.ToString();
    public string RiichiPolicy => game.config.riichiPolicy.ToString();
    public string DoraOptions => game.config.doraOption.ToString();
    public string AgariOptions => game.config.agariOption.ToString();
    public bool AllowsOpenTanyao => game.config.agariOption.HasFlag(AgariOption.Kuitan);
    public string ScoringOptions => game.config.scoringOption.ToString();
    public string RyuukyokuTriggers => game.config.ryuukyokuTrigger.ToString();
    public IReadOnlyList<string> AllowedYakus => game.config.AllowedYakus;

    /// <summary> Whether pulling North for a bonus han is enabled (3-player). </summary>
    public bool AllowsNukidora => game.config.doraOption.HasFlag(DoraOption.Nukidora);

    /// <summary> Whether kan reveals an additional dora indicator at all. </summary>
    public bool AllowsKanDora => game.config.doraOption.HasFlag(DoraOption.KanDora);

    /// <summary> Whether a kan also adds an uradora indicator (matters in riichi). </summary>
    public bool AllowsKanUradora => game.config.doraOption.HasFlag(DoraOption.KanUradora);

    /// <summary> Whether four kans by different players abort the hand. </summary>
    public bool AbortsOnFourKans =>
        game.config.ryuukyokuTrigger.HasFlag(RyuukyokuTrigger.SuukanSanra);
    #endregion

    #region Round / seat context (all public)
    public Wind RoundWind => game.info.wind;
    public Wind SelfWind => Self.Wind;
    public int Round => game.info.round;
    public int Dealer => game.info.dealer;
    public int Honba => game.info.honba;
    public int RiichiStick => game.info.riichiStick;
    public int CurrentPlayer => game.info.currentPlayer;
    public bool IsAllLast => game.info.IsAllLast;
    public bool SelfIsDealer => Self.IsDealer;
    public bool SelfFuriten => SelfHand.isFuriten;

    /// <summary> Nobody has drawn past their first turn yet (all still menzen). </summary>
    public bool IsFirstJun => game.IsFirstJun;

    /// <summary> The viewer's own turn number (1-based once the hand starts). </summary>
    public int SelfJun => SelfHand.jun;

    /// <summary> The viewer's own discard river. </summary>
    public IReadOnlyList<GameTile> SelfDiscards => SelfHand.discarded;

    /// <summary> How many North tiles the viewer has pulled (3-player nukidora). </summary>
    public int SelfNukiDoraCount => SelfHand.nukiDora.Count;
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

    /// <summary>
    /// The tile a seat declared riichi on, or null. Laid sideways on the table,
    /// so this is public information.
    /// </summary>
    public GameTile RiichiTileOf(int seat) => PlayerAt(seat).hand.riichiTile;

    /// <summary>
    /// The engine timestamp at which a seat declared riichi, or -1. Every tile
    /// discarded by anyone after this moment is safe against that seat (they
    /// would have ronned it), which is the strongest public defensive read.
    /// </summary>
    public int RiichiTimeOf(int seat) {
      var tile = RiichiTileOf(seat);
      return tile?.discardInfo?.time ?? -1;
    }

    /// <summary> Whether a seat declared riichi on its very first discard. </summary>
    public bool IsDoubleRiichi(int seat) => PlayerAt(seat).hand.wRiichi;

    /// <summary> Number of kans (of any kind) a seat has declared. </summary>
    public int KanCountOf(int seat) => PlayerAt(seat).hand.called.Count(m => m is Kan);

    /// <summary> Number of North tiles a seat has pulled (3-player nukidora). </summary>
    public int NukiDoraCountOf(int seat) => PlayerAt(seat).hand.nukiDora.Count;

    /// <summary> Total kans declared at the table so far. </summary>
    public int TotalKanCount => AllSeats.Sum(KanCountOf);

    /// <summary> Number of seats that have declared at least one kan. </summary>
    public int SeatsWithKan => AllSeats.Count(s => KanCountOf(s) > 0);

    /// <summary> Dora carried by a seat's OPEN melds and pulled North (public). </summary>
    public int VisibleDoraOf(int seat) {
      var hand = PlayerAt(seat).hand;
      int dora = 0;
      foreach (var meld in hand.called) {
        foreach (var tile in meld) {
          dora += CountDora(tile.tile) + (tile.tile.Akadora ? 1 : 0);
        }
      }
      dora += hand.nukiDora.Count;
      return dora;
    }

    /// <summary>
    /// Ranking of a seat by points, 1 = leader. Ties are broken by seat order so
    /// the result is a stable, total ordering (matching <c>Game.PlayersByRank</c>).
    /// </summary>
    public int RankOf(int seat) {
      return 1 + AllSeats.Count(s =>
          PointsOf(s) > PointsOf(seat) || (PointsOf(s) == PointsOf(seat) && s < seat));
    }

    /// <summary> The viewer's current placement, 1 = leader. </summary>
    public int SelfRank => RankOf(Seat);

    /// <summary>
    /// Points the viewer leads the next-best seat by (negative when trailing).
    /// </summary>
    public long SelfPointLead {
      get {
        var others = OpponentSeats.Select(PointsOf).ToList();
        return others.Count == 0 ? 0 : PointsOf(Seat) - others.Max();
      }
    }
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
    /// <param name="maxShanten">
    /// Stop searching past this shanten and report <see cref="int.MaxValue"/>
    /// instead. The resolver's cost grows sharply with the distance it may
    /// explore (<c>maxDist = min(13, maxShanten) + 1</c>), so a caller that only
    /// cares about the best few discards should say so: scoring all fourteen
    /// options uncapped costs several times as much as capping just above the
    /// hand's current shanten.
    /// </param>
    public DiscardEval EvaluateDiscard(
        GameTile discard, IReadOnlyList<GameTile> hand14,
        int maxShanten = int.MaxValue) {
      // A temporary hand bound to our own player (so hand.game resolves), holding
      // the tiles left after the discard, plus our real called melds.
      var remaining = hand14.Where(t => !ReferenceEquals(t, discard)).ToList();
      var tempHand = TempHand(remaining, SelfHand.called);

      // The resolver's acceptance computation (incoming == null) requires exactly
      // a 13-tile hand (melds count as 3 each). If the caller passed an
      // unexpected count, report an unreachable shanten rather than throwing.
      if (tempHand.Count != Game.HAND_SIZE) {
        return new DiscardEval(discard, int.MaxValue, 0);
      }

      var resolver = game.Get<PatternResolver>();
      int shanten =
          resolver.ResolveShanten(tempHand, null, out var acceptance, maxShanten);
      int ukeire = 0;
      if (acceptance != null) {
        foreach (var tile in acceptance) {
          ukeire += UnseenCount(tile);
        }
      }
      return new DiscardEval(discard, shanten, ukeire);
    }

    /// <summary>
    /// Builds a throwaway <see cref="Hand"/> for hypothetical analysis, bound to
    /// the viewer's own player so seat winds and the pattern resolver work.
    ///
    /// <see cref="Hand.menzen"/> is normally maintained by <c>Hand.AddGroup</c>;
    /// because we assign <c>called</c> directly it must be recomputed here, or an
    /// open hand would be scored with closed-hand yaku (riichi/pinfu/closed han
    /// values) and every min-han check built on it would be wrong.
    /// </summary>
    private Hand TempHand(
        IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called,
        GameTile riichiTile = null) {
      return new Hand {
        player = Self,
        freeTiles = [.. freeTiles],
        called = [.. called],
        menzen = called.All(meld => meld.IsClose),
        riichiTile = riichiTile,
      };
    }

    /// <summary>
    /// The tile kinds that advance a hypothetical 13-tile holding (its ukeire
    /// set), or an empty list when the holding is not a legal waiting size.
    /// </summary>
    public Tiles AcceptanceOf(
        IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called = null) {
      var tempHand = TempHand(freeTiles, called ?? SelfHand.called);
      if (tempHand.Count != Game.HAND_SIZE) {
        return Tiles.Empty;
      }
      game.Get<PatternResolver>().ResolveShanten(tempHand, null, out var acceptance);
      return acceptance ?? Tiles.Empty;
    }

    /// <summary>
    /// Total number of unseen tiles that advance a hypothetical 13-tile holding.
    /// </summary>
    public int UkeireOf(
        IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called = null) {
      return AcceptanceOf(freeTiles, called).Sum(UnseenCount);
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
      var tempHand = TempHand(freeTiles, called);
      // The resolver expects a 13-tile (waiting) or 14-tile (with incoming) hand,
      // melds counting as 3. For a post-call hand we evaluate the 13-tile wait.
      if (tempHand.Count is not Game.HAND_SIZE and not (Game.HAND_SIZE + 1)) {
        return int.MaxValue;
      }
      var resolver = game.Get<PatternResolver>();
      return resolver.ResolveShanten(tempHand, null, out _, Game.HAND_SIZE);
    }

    /// <summary>
    /// Current waits and guaranteed-minimum ron values when the viewer's
    /// 13-tile base hand is in tenpai. The pending draw is deliberately omitted:
    /// this describes the stable hand before the current draw/action.
    /// </summary>
    public IReadOnlyList<TenpaiInfo> SelfTenpaiInfos() {
      return TenpaiInfosOf(SelfHand.freeTiles, SelfHand.called, SelfHand.riichiTile);
    }

    /// <summary>
    /// Waits and per-wait scoring for a hypothetical 13-tile holding. Used to ask
    /// "if I take this kan / make this call, what can the resulting hand actually
    /// win with, and is it still legal under the table's minimum-han rule?".
    ///
    /// Returns an empty list when the holding is not tenpai. This is a relatively
    /// expensive call (a full yaku evaluation per wait), so callers should only
    /// run it on a handful of shortlisted candidates.
    /// </summary>
    public IReadOnlyList<TenpaiInfo> TenpaiInfosOf(
        IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called = null,
        GameTile riichiTile = null) {
      var tempHand = TempHand(freeTiles, called ?? SelfHand.called, riichiTile);
      if (tempHand.Count != Game.HAND_SIZE) return [];
      var resolver = game.Get<PatternResolver>();
      if (resolver.ResolveShanten(tempHand, null, out var waits, 0) != 0) return [];
      return waits.Select(wait =>
          PlayTileAction.ComputeTenpaiInfo(resolver, tempHand, wait)).ToList();
    }

    /// <summary>
    /// The best yaku han (excluding dora/bonus han, which never satisfies the
    /// table's minimum) reachable from a tenpai holding, counting only waits that
    /// still have live copies. Returns -1 when the holding is not tenpai or every
    /// wait is dead.
    /// </summary>
    public int BestLiveYakuHanOf(
        IReadOnlyList<GameTile> freeTiles, IReadOnlyList<MenLike> called = null,
        GameTile riichiTile = null) {
      var infos = TenpaiInfosOf(freeTiles, called, riichiTile);
      int best = -1;
      foreach (var info in infos) {
        if (UnseenCount(info.winningTile) <= 0) continue;
        int yaku = info.yakuman > 0 ? 13 * info.yakuman : info.yaku;
        if (yaku > best) best = yaku;
      }
      return best;
    }
  }
}
