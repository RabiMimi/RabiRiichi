using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace RabiRiichi.Patterns.Compiled {
  // Rule IDs are compiled once, rather than resolving types or invoking LINQ
  // callbacks for each candidate. RuleYaku nodes test their condition instead;
  // other user-defined rules use the legacy path.
  internal enum YakuRule {
    Unknown, Chankan, HaiteiRaoyue, HouteiRaoyui, Iipeikou, Ippatsu,
    MenzenchinTsumohou, Pinfu, Riichi, RinshanKaihou, Tanyao,
    YakuhaiBakaze, YakuhaiJikaze, YakuhaiChun, YakuhaiHaku, YakuhaiHatsu,
    Chantaiyao, Chiitoitsu, DoubleRiichi, Honroutou, Ittsu, Sanankou,
    Sankantsu, SanshokuDoujun, SanshokuDoukou, Shousangen, Toitoi,
    Honitsu, JunchanTaiyao, Ryanpeikou, Chinitsu, Fu13_1, Fu33332, Fu72,
    Chiihou, Chinroutou, ChuurenPoutou, Daisangen, Daisuushii, HelloWorld,
    JunseiChuurenPoutou, KokushiMusou, KokushiMusouJuusanmenMachi, Ryuuiisou,
    Shousuushii, Suuankou, SuuankouTanki, Suukantsu, Tenhou, Tsuuiisou,
    Benikujaku, Chaopaikou, Chinpaikou, Chinpeikou, Daichiishin, Daichikurin,
    Daisharin, Daisuurin, Heiiisou, IshiNoUeNiMoSannen, IsshokuSandoujun,
    IsshokuYondoujun, Kanburi, Katengecchi, Paarenchan, Renhou, Ryanankan,
    Sanankan, Sanfonkou, Sanrenkou, ShiiaruRaotai, Shiisanputa, Shiisuuputa,
    Shousanfon, Suurenkou, TsubameGaeshi, Uumensai,
    Akadora, Dora, NukiDora, Uradora,
  }

  /// <summary>
  /// A compiled graph of built-in scoring rules. Fork creates independent scratch
  /// storage so concurrent queries never share mutable scoring state.
  /// </summary>
  public sealed class CompiledScoringEvaluator {
    private sealed class Node {
      internal StdPattern Pattern;
      internal YakuRule Rule;
      internal RuleYaku Custom;
      internal bool Registered;
      internal int[] Ancestors, Required, Removes;
    }
    private readonly Node[] nodes;
    private readonly int[] roots;
    private readonly bool hasCustom;
    private readonly byte[] state;
    private readonly CompactScore[] contributions;
    private readonly WinView view = new();
    private CompactScore total;
    private BasePattern basePattern;
    private Hand hand;
    private GameTile incoming;
    private PatternMask excluded;
    private Features features;
    private int akadora, dora, uradora;
    public bool IsSupported { get; }
    /// <summary>Registered or dependency patterns without a compiled rule.</summary>
    public IReadOnlyList<StdPattern> UnsupportedPatterns { get; }

    public CompiledScoringEvaluator(IEnumerable<StdPattern> registered) {
      var active = registered.ToHashSet();
      var patterns = new List<StdPattern>();
      void Add(StdPattern p) {
        if (patterns.Contains(p)) return;
        patterns.Add(p);
        foreach (var parent in p.afterPatterns) Add(parent);
      }
      foreach (var p in active) Add(p);
      nodes = new Node[patterns.Count];
      var unsupported = new List<StdPattern>();
      for (int i = 0; i < nodes.Length; i++) {
        var p = patterns[i];
        var rule = YakuRule.Unknown;
        var custom = p as RuleYaku;
        bool known = custom != null || p.GetType().Assembly == typeof(StdPattern).Assembly
            && Enum.TryParse(p.GetType().Name, out rule) && rule != YakuRule.Unknown;
        if (!known || p.hasCustomPredicate) { unsupported.Add(p); rule = YakuRule.Unknown; custom = null; }
        // The graph below is read once; later edits to the rule would silently diverge.
        custom?.Freeze();
        var ancestors = p.afterPatterns.Select(q => patterns.IndexOf(q)).ToArray();
        nodes[i] = new Node {
          Pattern = p, Rule = rule, Custom = custom, Registered = active.Contains(p),
          Ancestors = ancestors,
          Removes = custom != null ? custom.Replaced.Select(q => patterns.IndexOf(q)).ToArray()
              : RemovesAfter(rule) ? ancestors : [],
          // Read from DependOn so the compiled graph cannot drift from the pattern classes.
          Required = p.dependencies.Select(q => patterns.IndexOf(q)).ToArray(),
        };
      }
      hasCustom = nodes.Any(n => n.Custom != null);
      roots = active.Select(p => patterns.IndexOf(p)).ToArray();
      state = new byte[nodes.Length]; contributions = new CompactScore[nodes.Length];
      UnsupportedPatterns = unsupported.AsReadOnly();
      IsSupported = unsupported.Count == 0;
    }

    private CompiledScoringEvaluator(CompiledScoringEvaluator plan) {
      nodes = plan.nodes; roots = plan.roots; IsSupported = plan.IsSupported; hasCustom = plan.hasCustom;
      UnsupportedPatterns = plan.UnsupportedPatterns;
      state = new byte[nodes.Length]; contributions = new CompactScore[nodes.Length];
    }
    public CompiledScoringEvaluator Fork() => new(this);

    // Mirrors the patterns whose Resolve calls scores.Remove(afterPatterns).
    private static bool RemovesAfter(YakuRule r) => r is YakuRule.Pinfu or YakuRule.DoubleRiichi
        or YakuRule.Honroutou or YakuRule.JunchanTaiyao or YakuRule.Ryanpeikou or YakuRule.Chinitsu
        or YakuRule.Chinroutou or YakuRule.ChuurenPoutou or YakuRule.Daisuushii or YakuRule.HelloWorld
        or YakuRule.JunseiChuurenPoutou or YakuRule.KokushiMusouJuusanmenMachi or YakuRule.Ryuuiisou
        or YakuRule.Shousuushii or YakuRule.Suuankou or YakuRule.SuuankouTanki or YakuRule.Suukantsu
        or YakuRule.Tsuuiisou or YakuRule.Benikujaku or YakuRule.Daichiishin or YakuRule.Daichikurin
        or YakuRule.Daisharin or YakuRule.Daisuurin or YakuRule.Heiiisou or YakuRule.IshiNoUeNiMoSannen
        or YakuRule.IsshokuSandoujun or YakuRule.IsshokuYondoujun or YakuRule.Katengecchi
        or YakuRule.Sanankan or YakuRule.Suurenkou;

    public CompactScore Evaluate(BasePattern family, Hand hand, GameTile incoming,
        ReadOnlySpan<CompactMeld> groups, ReadOnlySpan<byte> counts, int incomingGroup,
        int incomingTileIndex, PatternMask included, PatternMask excludedMask,
        int akadora, int dora, int uradora) {
      if (!IsSupported) throw new InvalidOperationException("This pattern set requires the custom-rule fallback");
      this.hand = hand; this.incoming = incoming; basePattern = family; excluded = excludedMask;
      this.akadora = akadora; this.dora = dora; this.uradora = uradora;
      features = Features.Build(groups, counts, incomingGroup, incomingTileIndex, incoming.IsTsumo);
      if (hasCustom) view.Set(hand, incoming, groups, counts, incomingGroup, incomingTileIndex);
      Array.Clear(state); Array.Clear(contributions); total = default;
      foreach (int root in roots) if ((nodes[root].Pattern.type & included) != 0) Resolve(root);
      return total;
    }

    private bool Resolve(int index) {
      var node = nodes[index];
      if ((node.Pattern.type & excluded) != 0) return false;
      if (state[index] != 0) return state[index] == 2;
      state[index] = 1;
      if (!node.Pattern.basePatterns.Contains(basePattern)) return false;
      foreach (int parent in node.Ancestors) Resolve(parent);
      foreach (int parent in node.Required) if (parent < 0 || state[parent] != 2) return false;
      CompactScore value;
      if (node.Custom != null) {
        if (!node.Custom.Condition.Test(view)) return false;
        value = node.Custom.Score(hand);
      } else if (!Apply(node.Rule, out value)) return false;
      state[index] = 2;
      if (node.Registered) {
        foreach (int removed in node.Removes) {
          AddTotals(contributions[removed], -1); contributions[removed] = default;
        }
        contributions[index] = value; AddTotals(value, 1);
      }
      return true;
    }

    private void AddTotals(CompactScore s, int sign) => total = new(total.Han + sign * s.Han,
        total.Yaku + sign * s.Yaku, total.Fu + sign * s.Fu,
        total.Yakuman + sign * s.Yakuman, total.BonusYakuman + sign * s.BonusYakuman);

    public ScoreStorage Materialize(ScoringOption option) {
      var result = new ScoreStorage();
      for (int i = 0; i < contributions.Length; i++) {
        var s = contributions[i]; var p = nodes[i].Pattern;
        if (s.Yaku != 0) result.Add(new(ScoringType.Han, s.Yaku, p));
        // Uradora deliberately reports a successful zero-valued entry too.
        if (s.Han != s.Yaku || nodes[i].Rule == YakuRule.Uradora && state[i] == 2 && nodes[i].Registered)
          result.Add(new(ScoringType.BonusHan, s.Han - s.Yaku, p));
        if (s.Fu != 0) result.Add(new(ScoringType.Fu, s.Fu, p));
        if (s.Yakuman != 0) result.Add(new(ScoringType.Yakuman, s.Yakuman, p));
        if (s.BonusYakuman != 0) result.Add(new(ScoringType.BonusYakuman, s.BonusYakuman, p));
      }
      result.Calc(option);
      return result;
    }

    private bool Apply(YakuRule rule, out CompactScore score) {
      var f = features;
      bool match = false;
      int han = 0, fu = 0, yakuman = 0, bonus = 0, bonusYakuman = 0;
      bool closed = hand.menzen;
      switch (rule) {
        case YakuRule.Chankan: match = incoming.discardInfo?.reason == DiscardReason.Chankan; han = 1; break;
        case YakuRule.HaiteiRaoyue: match = incoming.IsTsumo && hand.game.wall.IsHaitei && incoming.source == TileSource.Wall; han = 1; break;
        case YakuRule.HouteiRaoyui: match = !incoming.IsTsumo && hand.game.wall.IsHaitei; han = 1; break;
        case YakuRule.Iipeikou: match = closed && f.MaxSequenceCopies >= 2; han = 1; break;
        case YakuRule.Ippatsu: match = hand.ippatsu; han = 1; break;
        case YakuRule.MenzenchinTsumohou: match = closed && incoming.IsTsumo; han = 1; break;
        case YakuRule.Pinfu:
          match = closed && f.AllSequenceOrPair && f.IncomingKind == CompactMeldKind.Sequence
              && f.WaitFu == 0 && !hand.player.IsYaku(CompactMeld.TileAt(f.Pair));
          han = 1; fu = incoming.IsTsumo ? 20 : 30; break;
        case YakuRule.Riichi: match = hand.riichi; han = 1; break;
        case YakuRule.RinshanKaihou: match = incoming.source == TileSource.Wanpai; han = 1; break;
        case YakuRule.Tanyao: match = f.No19 && (closed || (hand.game.config.agariOption & AgariOption.Kuitan) != 0); han = 1; break;
        case YakuRule.YakuhaiBakaze: han = f.TripletCopies(27 + (int)hand.game.info.wind); match = han > 0; break;
        case YakuRule.YakuhaiJikaze: han = f.TripletCopies(27 + (int)hand.player.Wind); match = han > 0; break;
        case YakuRule.YakuhaiChun: han = f.TripletCopies(33); match = han > 0; break;
        case YakuRule.YakuhaiHaku: han = f.TripletCopies(31); match = han > 0; break;
        case YakuRule.YakuhaiHatsu: han = f.TripletCopies(32); match = han > 0; break;
        case YakuRule.Chantaiyao: match = f.EveryGroup19; han = closed ? 2 : 1; break;
        case YakuRule.Chiitoitsu: match = true; han = 2; break;
        case YakuRule.DoubleRiichi: match = hand.wRiichi; han = 2; break;
        case YakuRule.Honroutou: match = f.Sequences == 0 && f.Only19; han = 2; break;
        case YakuRule.Ittsu: match = f.HasIttsu; han = closed ? 2 : 1; break;
        case YakuRule.Sanankou: match = f.ClosedTriplets >= 3; han = 2; break;
        case YakuRule.Sankantsu: match = f.Quads >= 3; han = 2; break;
        case YakuRule.SanshokuDoujun: match = f.HasSanshoku(f.SequenceMask); han = closed ? 2 : 1; break;
        case YakuRule.SanshokuDoukou: match = f.HasSanshoku(f.TripletMask); han = 2; break;
        case YakuRule.Shousangen: match = f.Pair >= 31 && BitOperations.PopCount(f.FirstMask & (7UL << 31)) == 3; han = 2; break;
        case YakuRule.Toitoi: match = f.Sequences == 0; han = 2; break;
        case YakuRule.Honitsu: match = BitOperations.PopCount((uint)f.SuitedSuits) == 1; han = closed ? 3 : 2; break;
        case YakuRule.JunchanTaiyao: match = !f.HasHonors; han = closed ? 3 : 2; break;
        case YakuRule.Ryanpeikou: match = closed && f.Sequences == 4 && (f.SequenceTypes == 1 || f.SequenceTypes == 2 && f.MaxSequenceCopies == 2); han = 3; break;
        case YakuRule.Chinitsu: match = !f.HasHonors; han = closed ? 6 : 5; break;
        case YakuRule.Fu13_1: case YakuRule.Fu72: match = true; fu = 25; break;
        case YakuRule.Fu33332:
          match = true; fu = 20 + f.MeldFu + f.WaitFu + (incoming.IsTsumo ? 2 : closed ? 10 : 0);
          if (f.Pair >= 0) { var pair = CompactMeld.TileAt(f.Pair); if (hand.game.IsYaku(pair)) fu += 2; if (pair.IsSame(Tile.From(hand.player.Wind))) fu += 2; }
          fu = (Math.Max(30, fu) + 9) / 10 * 10; break;
        case YakuRule.Chiihou: match = incoming.IsTsumo && hand.game.IsFirstJun && !hand.player.IsDealer; yakuman = 1; break;
        case YakuRule.Chinroutou: match = true; yakuman = 1; break;
        case YakuRule.ChuurenPoutou: match = closed && f.Quads == 0 && f.NineGates; yakuman = 1; break;
        case YakuRule.Daisangen: match = BitOperations.PopCount(f.TripletMask & (7UL << 31)) == 3; yakuman = 1; break;
        case YakuRule.Daisuushii: match = BitOperations.PopCount(f.TripletMask & (15UL << 27)) == 4; yakuman = 2; break;
        case YakuRule.HelloWorld: match = closed && f.HelloWorld; yakuman = 3; break;
        case YakuRule.JunseiChuurenPoutou: match = hand.called.Count == 0 && f.PureNineGates; yakuman = 2; break;
        case YakuRule.KokushiMusou: match = true; yakuman = 1; break;
        case YakuRule.KokushiMusouJuusanmenMachi: match = f.IncomingCopies >= 2; yakuman = 2; break;
        case YakuRule.Ryuuiisou: match = (f.Present & ~TileMasks.Green) == 0; yakuman = 1; break;
        case YakuRule.Shousuushii: match = f.Pair is >= 27 and <= 30 && BitOperations.PopCount(f.FirstMask & (15UL << 27)) == 4; yakuman = 1; break;
        case YakuRule.Suuankou: match = f.ClosedTriplets == 4; yakuman = 1; break;
        case YakuRule.SuuankouTanki: match = f.IncomingKind == CompactMeldKind.Pair; yakuman = 2; break;
        case YakuRule.Suukantsu: match = f.Quads == 4; yakuman = 1; break;
        case YakuRule.Tenhou: match = incoming.IsTsumo && hand.jun == 1 && hand.player.IsDealer; yakuman = 1; break;
        case YakuRule.Tsuuiisou: match = f.SuitedSuits == 0; yakuman = 1; break;
        case YakuRule.Benikujaku: match = (f.Present & ~TileMasks.Red) == 0; yakuman = 1; break;
        case YakuRule.Heiiisou: match = (f.Present & ~TileMasks.Black) == 0; yakuman = 1; break;
        case YakuRule.Chaopaikou: match = f.KouSeries(3, 2); han = 2; break;
        case YakuRule.Chinpaikou: match = f.KouSeries(3, 3); han = 2; break;
        case YakuRule.Sanrenkou: match = f.KouSeries(3, 1); han = 2; break;
        case YakuRule.Suurenkou: match = f.KouSeries(4, 1); yakuman = 1; break;
        case YakuRule.Chinpeikou: match = closed && f.SequenceTypes == 2 && f.SameSequenceRanks; han = 3; break;
        case YakuRule.Daichiishin: match = closed; yakuman = 2; break;
        case YakuRule.Daisharin: match = closed && f.Wheel(1); yakuman = 1; break;
        case YakuRule.Daichikurin: match = closed && f.Wheel(2); yakuman = 1; break;
        case YakuRule.Daisuurin: match = closed && f.Wheel(0); yakuman = 1; break;
        case YakuRule.IshiNoUeNiMoSannen: match = hand.game.wall.IsHaitei; yakuman = 1; break;
        case YakuRule.IsshokuSandoujun: match = f.MaxSequenceCopies >= 3; han = closed ? 3 : 2; break;
        case YakuRule.IsshokuYondoujun: match = f.MaxSequenceCopies >= 4; yakuman = 1; break;
        case YakuRule.Kanburi: match = incoming.discardInfo?.reason == DiscardReason.DrawRinshan; han = 1; break;
        case YakuRule.Katengecchi: match = hand.game.wall.IsHaitei; yakuman = 1; break;
        case YakuRule.Paarenchan: match = hand.player.IsDealer && hand.player.agariStreak + 1 >= Paarenchan.REQUIRED_STREAK; bonusYakuman = 1; break;
        case YakuRule.Renhou: match = !incoming.IsTsumo && !hand.player.IsDealer && hand.jun == 0 && hand.game.IsFirstJun; yakuman = 1; break;
        case YakuRule.Ryanankan: match = f.ClosedQuads >= 2; han = 2; break;
        case YakuRule.Sanankan: match = f.ClosedQuads >= 3; yakuman = 1; break;
        case YakuRule.Sanfonkou: match = f.WindTriplets >= 3; han = 2; break;
        case YakuRule.ShiiaruRaotai: match = hand.freeTiles.Count == 1; han = 1; break;
        case YakuRule.Shiisanputa: match = f.Pair >= 0; yakuman = 1; break;
        case YakuRule.Shiisuuputa: match = f.AllSingles; yakuman = 1; break;
        case YakuRule.Shousanfon: match = f.Pair is >= 27 and <= 30 && f.WindTriplets >= 2; han = 1; break;
        case YakuRule.TsubameGaeshi: match = incoming.discardInfo?.isRiichiDeclaration == true; han = 1; break;
        case YakuRule.Uumensai: match = f.SuitedSuits == 7 && (f.FirstMask & (15UL << 27)) != 0 && (f.FirstMask & (7UL << 31)) != 0; han = 2; break;
        case YakuRule.Akadora: bonus = akadora; match = bonus > 0; break;
        case YakuRule.Dora: bonus = dora; match = bonus > 0; break;
        case YakuRule.NukiDora: bonus = hand.nukiDora.Count; match = bonus > 0; break;
        case YakuRule.Uradora: bonus = uradora; match = hand.riichi; break;
      }
      score = match ? new(han + bonus, han, fu, yakuman, bonusYakuman) : default;
      return match;
    }

    private struct Features {
      internal ulong Present, FirstMask, SequenceMask, TripletMask;
      internal UInt128 TripletMultiplicities;
      internal int Pair, Sequences, SequenceTypes, MaxSequenceCopies, ClosedTriplets, Quads, ClosedQuads, WindTriplets;
      internal int SuitedSuits, MeldFu, WaitFu, IncomingCopies;
      internal bool No19, Only19, EveryGroup19, HasHonors, AllSequenceOrPair, AllSingles;
      internal bool HasIttsu, SameSequenceRanks, NineGates, PureNineGates, HelloWorld;
      internal CompactMeldKind IncomingKind;
      internal int TripletCopies(int i) => (int)((TripletMultiplicities >> (i * 3)) & 7);
      internal bool HasSanshoku(ulong mask) => (mask & (mask >> 9) & (mask >> 18) & 511) != 0;
      internal bool Wheel(int suit) => (Present & ~((ulong)254 << (suit * 9))) == 0;
      internal bool KouSeries(int count, int step) {
        for (int suit = 0; suit < 3; suit++) {
          ulong mask = (TripletMask >> (suit * 9)) & 511;
          ulong starts = mask;
          for (int n = 1; n < count; n++) starts &= mask >> (n * step);
          if (starts != 0) return true;
        }
        return false;
      }
      internal static bool Terminal(int i) => i < 27 && i % 9 is 0 or 8;
      internal static bool TerminalOrHonor(int i) => i >= 27 || Terminal(i);

      internal static Features Build(ReadOnlySpan<CompactMeld> groups, ReadOnlySpan<byte> counts,
          int incomingGroup, int incomingTile, bool tsumo) {
        var f = new Features { Pair = -1, No19 = true, Only19 = true, EveryGroup19 = true, AllSequenceOrPair = true, AllSingles = true };
        UInt128 sequences = 0;
        for (int n = 0; n < groups.Length; n++) {
          var g = groups[n]; int i = g.TileIndex;
          f.FirstMask |= 1UL << i;
          f.AllSingles &= g.Kind == CompactMeldKind.Single;
          f.AllSequenceOrPair &= g.Kind is CompactMeldKind.Sequence or CompactMeldKind.Pair;
          f.EveryGroup19 &= TerminalOrHonor(i) || g.Kind == CompactMeldKind.Sequence && i % 9 == 6;
          if (g.Kind == CompactMeldKind.Pair) f.Pair = i;
          if (g.Kind == CompactMeldKind.Sequence) {
            f.Sequences++; f.SequenceMask |= 1UL << i;
            sequences += (UInt128)1 << (i * 3);
            f.MaxSequenceCopies = Math.Max(f.MaxSequenceCopies, (int)((sequences >> (i * 3)) & 7));
          }
          if (g.Kind is CompactMeldKind.Triplet or CompactMeldKind.Quad) {
            bool close = g.IsClose && (n != incomingGroup || tsumo);
            f.TripletMask |= 1UL << i; f.TripletMultiplicities += (UInt128)1 << (i * 3);
            if (close) f.ClosedTriplets++;
            if (i is >= 27 and <= 30) f.WindTriplets++;
            if (g.Kind == CompactMeldKind.Quad) { f.Quads++; if (close) f.ClosedQuads++; }
            f.MeldFu += (TerminalOrHonor(i) ? 4 : 2) * (close ? 2 : 1) * (g.Kind == CompactMeldKind.Quad ? 4 : 1);
          }
        }
        f.SequenceTypes = BitOperations.PopCount(f.SequenceMask);
        int firstSequence = -1, secondSequence = -1;
        for (int i = 0; i < 27; i++) if ((f.SequenceMask & (1UL << i)) != 0) { if (firstSequence < 0) firstSequence = i; else secondSequence = i; }
        f.SameSequenceRanks = firstSequence >= 0 && secondSequence >= 0 && firstSequence / 9 != secondSequence / 9 && firstSequence % 9 == secondSequence % 9;
        for (int suit = 0; suit < 3; suit++) if (((f.SequenceMask >> (suit * 9)) & 73) == 73) f.HasIttsu = true;
        ulong rankCounts = 0;
        for (int i = 0; i < counts.Length; i++) if (counts[i] != 0) {
          f.Present |= 1UL << i; rankCounts += (ulong)counts[i] << ((i % 9) * 5);
          if (i < 27) f.SuitedSuits |= 1 << (i / 9); else f.HasHonors = true;
          if (TerminalOrHonor(i)) f.No19 = false; else f.Only19 = false;
        }
        ulong gates = 3UL | (3UL << 40);
        for (int r = 1; r < 8; r++) gates |= 1UL << (r * 5);
        f.NineGates = BitOperations.PopCount((uint)f.SuitedSuits) == 1 && !f.HasHonors;
        for (int r = 0; r < 9; r++) f.NineGates &= ((rankCounts >> (r * 5)) & 31) >= (r is 0 or 8 ? 3UL : 1UL);
        f.PureNineGates = rankCounts - (1UL << (incomingTile % 9 * 5)) == gates;
        ulong hello = (3UL << 5) | (4UL << 25) | (3UL << 30) | (1UL << 35) | (3UL << 40);
        f.HelloWorld = rankCounts == hello;
        var winning = groups[incomingGroup]; f.IncomingKind = winning.Kind;
        f.IncomingCopies = counts[incomingTile];
        if (winning.Kind == CompactMeldKind.Pair) f.WaitFu = 2;
        if (winning.Kind == CompactMeldKind.Sequence) {
          int start = winning.TileIndex;
          f.WaitFu = incomingTile == start + 1 || start % 9 == 0 && incomingTile != start || start % 9 == 6 && incomingTile != start + 2 ? 2 : 0;
        }
        return f;
      }
    }
  }
}
