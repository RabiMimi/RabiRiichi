using RabiRiichi.Core;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// How dangerous one opponent looks, and everything needed to price a discard
  /// against them. Built entirely from public information.
  /// </summary>
  public sealed class SeatThreat {
    public int Seat { get; init; }

    /// <summary> Declared riichi: a known tenpai with an unknown, locked wait. </summary>
    public bool Riichi { get; init; }

    /// <summary>
    /// Probability-ish estimate in [0,1] that this seat is tenpai right now.
    /// Riichi is 1; an open hand is read from melds, turn count and discards.
    /// </summary>
    public double TenpaiOdds { get; init; }

    /// <summary>
    /// Estimated points we would lose by dealing in, used to weigh how much a
    /// risky discard actually costs.
    /// </summary>
    public double ExpectedValue { get; init; }

    /// <summary>
    /// Tiles that are 100% safe against this seat: their own discards, plus
    /// everything discarded by anybody after they declared riichi (they would
    /// have called ron on it).
    /// </summary>
    public IReadOnlyCollection<byte> Genbutsu { get; init; }

    /// <summary> Overall weight of this threat, folding in tenpai odds and value. </summary>
    public double Weight => TenpaiOdds * (ExpectedValue / 5000.0);
  }

  /// <summary>
  /// Reads the table for danger and prices individual discards.
  ///
  /// The rule-based agent may only use public information, which rules out the
  /// usual "peek at the opponent's wait" shortcut. Everything here is therefore
  /// classical human defence: genbutsu, suji, kabe/one-chance, honor counting,
  /// meld and dora reading, and turn-based tenpai estimation.
  /// </summary>
  public static class DefenseModel {
    /// <summary>
    /// Builds a threat entry for every opponent that could plausibly be tenpai.
    /// Seats that look harmless are omitted.
    /// </summary>
    public static IReadOnlyList<SeatThreat> Threats(PublicGameView view) {
      var threats = new List<SeatThreat>();
      foreach (var seat in view.OpponentSeats) {
        double odds = TenpaiOdds(view, seat);
        if (odds <= 0.05) {
          continue;
        }
        threats.Add(new SeatThreat {
          Seat = seat,
          Riichi = view.IsRiichi(seat),
          TenpaiOdds = odds,
          ExpectedValue = ExpectedValue(view, seat),
          Genbutsu = SafeTiles(view, seat),
        });
      }
      return threats;
    }

    /// <summary> Threats restricted to seats that have declared riichi. </summary>
    public static IReadOnlyList<SeatThreat> RiichiThreats(PublicGameView view) =>
        [.. Threats(view).Where(t => t.Riichi)];

    #region Tenpai / value reading

    /// <summary>
    /// Estimates the chance a seat is tenpai. Riichi is certain. Otherwise we
    /// combine the classic public tells: how many melds they have called, how
    /// valuable those melds are, how late it is, and whether their recent
    /// discards look like a settled hand (honors and terminals early, then
    /// middle tiles coming out of the hand).
    /// </summary>
    public static double TenpaiOdds(PublicGameView view, int seat) {
      if (view.IsRiichi(seat)) {
        return 1.0;
      }
      int jun = view.JunOf(seat);
      if (jun <= 3) {
        return 0.0;
      }

      var melds = view.CalledOf(seat);
      int openMelds = melds.Count(m => !m.IsClose);
      int kans = view.KanCountOf(seat);

      // A concealed, silent hand late in the round is a real damaten risk, but a
      // much smaller one than an opponent who has committed by calling.
      double odds = 0.02 * System.Math.Max(0, jun - 5);
      odds += 0.10 * openMelds;
      odds += 0.10 * kans;

      // Calls only make sense with a yaku, so a called yakuhai or a dora-laden
      // meld means the hand is both real and worth something.
      if (openMelds > 0) {
        odds += 0.06 * view.VisibleDoraOf(seat);
        if (melds.Any(m => m is Kou or Kan && IsValueHonorFor(view, seat, m.First.tile))) {
          odds += 0.08;
        }
      }

      // Late tedashi of a middle tile usually means the hand has settled and is
      // discarding from a completed shape rather than filling one.
      odds += 0.05 * LateTedashiCount(view, seat);
      return System.Math.Clamp(odds, 0, 0.95);
    }

    /// <summary>
    /// Estimated points a deal-in to this seat would cost, from public evidence
    /// only: riichi and its bonus han, visible dora in melds, dealer status.
    /// </summary>
    public static double ExpectedValue(PublicGameView view, int seat) {
      // Baseline non-dealer ron on a plain hand.
      double han = 1;
      if (view.IsRiichi(seat)) {
        // Riichi, plus the usual uradora/ippatsu/tsumo tail that riichi carries.
        han += 2.2;
      }
      han += view.VisibleDoraOf(seat);
      // Unseen dora in a concealed hand: on average a hand holds a share of the
      // remaining dora, and a riichi that pushed is more likely to hold some.
      han += view.RevealedDoraIndicators.Count * 0.35;
      if (view.KanCountOf(seat) > 0) {
        han += view.KanCountOf(seat) * 0.8;
      }

      // Rough han -> points curve for a ron (non-dealer), then the dealer bump.
      double points = han switch {
        <= 1 => 1300,
        <= 2 => 2600,
        <= 3 => 5200,
        <= 4 => 7700,
        <= 5 => 8000,
        <= 7 => 12000,
        <= 10 => 16000,
        _ => 24000,
      };
      if (view.IsDealer(seat)) {
        points *= 1.5;
      }
      return points;
    }

    private static bool IsValueHonorFor(PublicGameView view, int seat, Tile tile) {
      if (!tile.IsZ) return false;
      if (tile.IsSangen) return true;
      return tile.IsSame(Tile.From(view.RoundWind)) ||
          tile.IsSame(Tile.From(view.WindOf(seat)));
    }

    /// <summary>
    /// Counts discards from turn 7 onwards that came out of the hand rather than
    /// straight off the draw and were live middle tiles - the standard read that
    /// a silent opponent has reached tenpai.
    /// </summary>
    private static int LateTedashiCount(PublicGameView view, int seat) {
      int count = 0;
      foreach (var tile in view.DiscardsOf(seat)) {
        var info = tile.discardInfo;
        if (info == null || info.jun < 7) continue;
        // drawnJun == the turn the tile entered the hand; equal to the discard
        // turn means it was drawn and thrown (tsumogiri), which tells us nothing.
        bool tedashi = tile.drawnJun != info.jun;
        if (tedashi && tile.tile.IsMPS && tile.tile.Num is >= 3 and <= 7) {
          count++;
        }
      }
      return System.Math.Min(count, 3);
    }

    #endregion

    #region Safe tiles

    /// <summary>
    /// The set of tile kinds (akadora-normalized <see cref="Tile.NoDoraVal"/>)
    /// that cannot deal into this seat.
    ///
    /// Two sources, both public:
    ///  - the seat's own discard river (furiten);
    ///  - for a riichi seat, everything discarded by ANY player after the riichi
    ///    declaration, because a riichi hand cannot decline a ron.
    /// </summary>
    public static IReadOnlyCollection<byte> SafeTiles(PublicGameView view, int seat) {
      var safe = new HashSet<byte>();
      foreach (var tile in view.DiscardsOf(seat)) {
        safe.Add(tile.tile.NoDoraVal);
      }

      int riichiTime = view.RiichiTimeOf(seat);
      if (riichiTime < 0) {
        return safe;
      }
      foreach (var other in view.AllSeats) {
        foreach (var tile in view.DiscardsOf(other)) {
          if ((tile.discardInfo?.time ?? -1) > riichiTime) {
            safe.Add(tile.tile.NoDoraVal);
          }
        }
      }
      return safe;
    }

    #endregion

    #region Per-tile danger

    /// <summary>
    /// Probability-ish estimate in [0,1] that discarding <paramref name="tile"/>
    /// deals into <paramref name="threat"/>, before scaling by tenpai odds.
    /// </summary>
    public static double DealInChance(PublicGameView view, SeatThreat threat, Tile tile) {
      if (threat.Genbutsu.Contains(tile.NoDoraVal)) {
        return 0;
      }

      double chance = tile.IsZ
          ? HonorDanger(view, threat.Seat, tile)
          : NumberDanger(view, threat.Seat, tile);

      // A non-riichi hand may not be tenpai at all and may also simply decline a
      // ron that does not meet the table minimum, so treat it as less sharp.
      if (!threat.Riichi) {
        chance *= 0.75;
      }
      // A riichi declared very early is more likely on a wide two-sided wait,
      // which makes suji and kabe reads more valuable; a late riichi is often a
      // narrow or unusual wait, so the same reads are less trustworthy.
      if (threat.Riichi) {
        int jun = view.JunOf(threat.Seat);
        chance *= jun >= 10 ? 1.15 : 1.0;
      }
      return System.Math.Clamp(chance, 0, 1);
    }

    /// <summary>
    /// The chance a discard deals into anybody, weighted by how likely each seat
    /// is tenpai. Higher is worse.
    /// </summary>
    public static double DealInRisk(
        PublicGameView view, IReadOnlyList<SeatThreat> threats, Tile tile) {
      double survive = 1.0;
      foreach (var threat in threats) {
        survive *= 1 - DealInChance(view, threat, tile) * threat.TenpaiOdds;
      }
      return 1 - survive;
    }

    /// <summary>
    /// Expected points lost by discarding a tile this turn: the chance of dealing
    /// in to each seat times what that seat is worth.
    /// </summary>
    public static double ExpectedLoss(
        PublicGameView view, IReadOnlyList<SeatThreat> threats, Tile tile) {
      double loss = 0;
      foreach (var threat in threats) {
        loss += DealInChance(view, threat, tile) * threat.TenpaiOdds * threat.ExpectedValue;
      }
      return loss;
    }

    /// <summary>
    /// Honors can only be hit by a pair wait or a lone-tile wait, so danger falls
    /// off sharply with how many copies are already visible. A yakuhai for that
    /// seat is more dangerous because they are more likely to have kept a pair.
    /// </summary>
    private static double HonorDanger(PublicGameView view, int seat, Tile tile) {
      int seen = view.VisibleCount(tile);
      double baseDanger = seen switch {
        >= 3 => 0.010,
        2 => 0.025,
        1 => 0.045,
        _ => 0.055,
      };
      if (IsValueHonorFor(view, seat, tile)) {
        baseDanger *= 1.5;
      }
      return baseDanger;
    }

    /// <summary>
    /// Number-tile danger from suji, kabe (walls of visible tiles) and distance
    /// from the middle of the suit.
    /// </summary>
    private static double NumberDanger(PublicGameView view, int seat, Tile tile) {
      int n = tile.Num;

      // Raw danger by number, the standard "no information" deal-in table.
      double danger = n switch {
        1 or 9 => 0.045,
        2 or 8 => 0.055,
        3 or 7 => 0.070,
        _ => 0.075,
      };

      if (HasSuji(view, seat, tile)) {
        // A two-sided wait on this tile is impossible; only closed, edge and pair
        // waits remain. 4-6 need both halves and are far safer when they have them.
        danger *= n is >= 4 and <= 6 ? 0.35 : 0.45;
      }

      // Kabe / one-chance: a two-sided wait on this tile needs the neighbouring
      // number to exist in someone's hand. If every copy is already on the table
      // that shape cannot exist.
      int below = n > 1 ? view.VisibleCount(new Tile(tile.Suit, n - 1)) : 4;
      int above = n < 9 ? view.VisibleCount(new Tile(tile.Suit, n + 1)) : 4;
      if (below >= 4 && above >= 4) {
        danger *= 0.30; // no-chance
      } else if (below >= 4 || above >= 4) {
        danger *= 0.65; // one side walled off
      } else if (below == 3 || above == 3) {
        danger *= 0.85; // one-chance
      }

      // Copies of the tile itself that are already visible cannot be waited on as
      // a pair, and make the remaining shapes rarer.
      int seenSelf = view.VisibleCount(tile);
      if (seenSelf >= 3) {
        danger *= 0.70;
      }
      return danger;
    }

    /// <summary>
    /// Whether a two-sided wait on <paramref name="tile"/> is ruled out by the
    /// seat's own discards (classic suji). 4-6 require both halves.
    /// </summary>
    public static bool HasSuji(PublicGameView view, int seat, Tile tile) {
      if (!tile.IsMPS) {
        return false;
      }
      int n = tile.Num;
      var suit = tile.Suit;
      bool DiscardedNum(int num) =>
          num is >= 1 and <= 9
          && view.DiscardsOf(seat).Any(t => {
            var dt = t.tile;
            return dt.Suit == suit && dt.Num == num;
          });

      return n switch {
        1 or 2 or 3 => DiscardedNum(n + 3),
        7 or 8 or 9 => DiscardedNum(n - 3),
        _ => DiscardedNum(n - 3) && DiscardedNum(n + 3),
      };
    }

    #endregion
  }
}
