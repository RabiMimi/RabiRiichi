using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  [Flags]
  public enum GroupKinds {
    None = 0, Single = 1, Pair = 2, Sequence = 4, Triplet = 8, Quad = 16,
    Meld = Sequence | Triplet | Quad, Any = Single | Pair | Meld,
  }

  public enum WaitShape { Ryanmen, Kanchan, Penchan, Shanpon, Tanki, Single }

  /// <summary>
  /// A yaku requirement. <see cref="Test"/> decides a complete interpretation;
  /// <see cref="MayHold"/> and <see cref="MayFail"/> decide whether some
  /// completion of a partial wildcard target could satisfy or violate it. The
  /// partial tests may answer true when unsure but must never answer false
  /// wrongly: a false MayHold prunes every completion of that target.
  /// Subclass to add a requirement kind; the defaults are always admissible.
  /// </summary>
  public abstract class YakuCondition {
    public abstract bool Test(WinView win);
    public virtual bool MayHold(PartialView partial) => true;
    public virtual bool MayFail(PartialView partial) => true;

    public static YakuCondition operator &(YakuCondition a, YakuCondition b) => Cond.All(a, b);
    public static YakuCondition operator |(YakuCondition a, YakuCondition b) => Cond.Any(a, b);
    public static YakuCondition operator !(YakuCondition a) => Cond.Not(a);
  }

  /// <summary>Selects groups by kind, tiles and closedness. Repeated narrowing calls intersect.</summary>
  public sealed class GroupFilter {
    private const ulong AllTiles = (1UL << 34) - 1;
    private readonly GroupKinds kinds;
    private readonly ulong within, containing, starting;
    private readonly bool? closed;
    /// <summary>Kinds a free (future) group could take while matching the tile constraints.</summary>
    internal readonly GroupKinds FutureKinds;

    internal GroupFilter(GroupKinds kinds, ulong within = AllTiles, ulong containing = AllTiles,
        ulong starting = AllTiles, bool? closed = null) {
      this.kinds = kinds; this.within = within; this.containing = containing; this.starting = starting; this.closed = closed;
      for (int i = 0; i < 34; i++) {
        if (ShapeMatches(new(CompactMeldKind.Single, i, true))) FutureKinds |= GroupKinds.Single;
        if (ShapeMatches(new(CompactMeldKind.Pair, i, true))) FutureKinds |= GroupKinds.Pair;
        if (ShapeMatches(new(CompactMeldKind.Triplet, i, true))) FutureKinds |= GroupKinds.Triplet;
        if (i < 27 && i % 9 < 7 && ShapeMatches(new(CompactMeldKind.Sequence, i, true))) FutureKinds |= GroupKinds.Sequence;
      }
    }

    /// <summary>Every tile of the group is in the set.</summary>
    public GroupFilter Of(Tiles tiles) => new(kinds, within & Cond.Mask(tiles), containing, starting, closed);
    public GroupFilter Of(string tiles) => Of(new Tiles(tiles));
    /// <summary>Some tile of the group is in the set.</summary>
    public GroupFilter Containing(Tiles tiles) => new(kinds, within, containing & Cond.Mask(tiles), starting, closed);
    public GroupFilter Containing(string tiles) => Containing(new Tiles(tiles));
    /// <summary>The group's lowest tile is in the set.</summary>
    public GroupFilter StartingAt(Tiles tiles) => new(kinds, within, containing, starting & Cond.Mask(tiles), closed);
    public GroupFilter StartingAt(string tiles) => StartingAt(new Tiles(tiles));
    /// <summary>Concealed groups. A group completed by a discarded tile counts as open.</summary>
    public GroupFilter Closed() => new(kinds, within, containing, starting, true);
    public GroupFilter Open() => new(kinds, within, containing, starting, false);

    private static GroupKinds KindOf(CompactMeld g) => g.Kind switch {
      CompactMeldKind.Single => GroupKinds.Single, CompactMeldKind.Pair => GroupKinds.Pair,
      CompactMeldKind.Sequence => GroupKinds.Sequence, CompactMeldKind.Triplet => GroupKinds.Triplet,
      _ => GroupKinds.Quad,
    };

    private bool ShapeMatches(CompactMeld g) {
      ulong tiles = YakuView.TileMask(g);
      return (kinds & KindOf(g)) != 0 && (tiles & ~within) == 0 && (tiles & containing) != 0
          && (starting & (1UL << g.TileIndex)) != 0;
    }

    internal bool Matches(CompactMeld g) => ShapeMatches(g) && (closed == null || closed == g.IsClose);

    internal int CountIn(WinView win) {
      int n = 0;
      for (int i = 0; i < win.GroupCount; i++) if (Matches(win.GroupAt(i))) n++;
      return n;
    }

    /// <summary>Bounds on the number of matching groups over every completion.</summary>
    internal void Range(PartialView v, out int lo, out int hi) {
      // Under ron, one closed free group (placed or future) becomes the open winning group.
      int must = 0, swing = 0;
      for (int n = 0; n < v.GroupCount; n++) {
        var g = v.GroupAt(n);
        if (!ShapeMatches(g)) continue;
        bool flexible = g.IsClose && !g.IsCalled && !v.IsTsumo;
        if (closed == null || !flexible && closed == g.IsClose) must++;
        else if (flexible) swing++;
      }
      int future = v.FutureGroups(FutureKinds);
      switch (closed) {
        case null: lo = must; hi = must + future; break;
        case true: lo = must + Math.Max(0, swing - 1); hi = must + swing + future; break;
        default: lo = must; hi = must + (!v.IsTsumo && swing + future > 0 ? 1 : 0); break;
      }
    }

    /// <summary>Whether a placed group matches in some completion.</summary>
    internal bool MayMatch(PartialView v, CompactMeld g) => ShapeMatches(g)
        && (closed == null || closed == g.IsClose || g.IsClose && !g.IsCalled && !v.IsTsumo);
    /// <summary>Whether a placed group fails to match in some completion.</summary>
    internal bool MayMiss(PartialView v, CompactMeld g) => !ShapeMatches(g)
        || closed != null && (closed != g.IsClose || g.IsClose && !g.IsCalled && !v.IsTsumo);
  }

  /// <summary>Starting points for <see cref="GroupFilter"/>.</summary>
  public static class Group {
    public static GroupFilter Any => new(GroupKinds.Any);
    public static GroupFilter Meld => new(GroupKinds.Meld);
    public static GroupFilter Sequence => new(GroupKinds.Sequence);
    /// <summary>Triplets, quads included.</summary>
    public static GroupFilter Triplet => new(GroupKinds.Triplet | GroupKinds.Quad);
    public static GroupFilter Quad => new(GroupKinds.Quad);
    public static GroupFilter Pair => new(GroupKinds.Pair);
    public static GroupFilter Single => new(GroupKinds.Single);
  }

  /// <summary>A count compared with integers to form a condition.</summary>
  public abstract class CountExpression {
    internal abstract YakuCondition Between(int min, int max);
    public YakuCondition AtLeast(int n) => Between(n, int.MaxValue);
    public YakuCondition AtMost(int n) => Between(int.MinValue, n);
    public YakuCondition Exactly(int n) => Between(n, n);
    public static YakuCondition operator >=(CountExpression c, int n) => c.AtLeast(n);
    public static YakuCondition operator <=(CountExpression c, int n) => c.AtMost(n);
    public static YakuCondition operator >(CountExpression c, int n) => c.AtLeast(n + 1);
    public static YakuCondition operator <(CountExpression c, int n) => c.AtMost(n - 1);
  }

  /// <summary>Builds <see cref="YakuCondition"/>s over effective tiles, groups and win context.</summary>
  public static class Cond {
    public static readonly YakuCondition True = Context(_ => true);
    public static readonly YakuCondition Closed = Context(v => v.Hand.menzen);
    public static readonly YakuCondition Tsumo = Context(v => v.IsTsumo);
    public static readonly YakuCondition Riichi = Context(v => v.Hand.riichi);
    public static readonly YakuCondition DoubleRiichi = Context(v => v.Hand.riichi && v.Hand.wRiichi);
    public static readonly YakuCondition Ippatsu = Context(v => v.Hand.ippatsu);
    public static readonly YakuCondition Dealer = Context(v => v.Hand.player.IsDealer);
    /// <summary>The wall is exhausted (haitei or houtei).</summary>
    public static readonly YakuCondition LastTile = Context(v => v.Hand.game.wall.IsHaitei);
    public static readonly YakuCondition FirstTurn = Context(v => v.Hand.game.IsFirstJun);

    public static YakuCondition From(TileSource source) => Context(v => v.Source == source);
    public static YakuCondition Discarded(DiscardReason reason) => Context(v => v.DiscardReason == reason);
    /// <summary>A test on win context only; it must not inspect tile identities.</summary>
    public static YakuCondition Context(Func<YakuView, bool> test) => new ContextCondition(test);

    public static YakuCondition All(params YakuCondition[] parts) => parts.Length == 1 ? parts[0]
        : new AllCondition(parts.SelectMany(p => p is AllCondition all ? all.Parts : [p]).ToArray());
    public static YakuCondition Any(params YakuCondition[] parts) => parts.Length == 1 ? parts[0]
        : new AnyCondition(parts.SelectMany(p => p is AnyCondition any ? any.Parts : [p]).ToArray());
    public static YakuCondition Not(YakuCondition condition) => condition is NotCondition not ? not.Inner : new NotCondition(condition);

    /// <summary>Every tile, called melds included, is in the set.</summary>
    public static YakuCondition AllTilesIn(Tiles tiles) => new TileSetCondition(Mask(tiles), true);
    public static YakuCondition AllTilesIn(string tiles) => AllTilesIn(new Tiles(tiles));
    public static YakuCondition AnyTileIn(Tiles tiles) => new TileSetCondition(Mask(tiles), false);
    public static YakuCondition AnyTileIn(string tiles) => AnyTileIn(new Tiles(tiles));
    /// <summary>Exactly one suit, optionally with honors.</summary>
    public static YakuCondition OneSuit(bool allowHonors) => Any([.. new[] { "123456789m", "123456789p", "123456789s" }
        .Select(suit => AllTilesIn(allowHonors ? suit + "1234567z" : suit) & AnyTileIn(suit))]);
    /// <summary>Copies of tiles in the set, counted across every group.</summary>
    public static CountExpression TileCount(Tiles tiles) => new TileCountExpression(Mask(tiles));
    public static CountExpression TileCount(string tiles) => TileCount(new Tiles(tiles));
    public static YakuCondition WinningTile(Tiles tiles) => new WinningTileCondition(Mask(tiles));
    public static YakuCondition WinningTile(string tiles) => WinningTile(new Tiles(tiles));

    public static CountExpression Count(GroupFilter filter) => new GroupCountExpression(filter);
    public static YakuCondition No(GroupFilter filter) => Count(filter).AtMost(0);
    public static YakuCondition Every(GroupFilter filter) => new EveryCondition(filter);
    /// <summary>The group completed by the winning tile matches.</summary>
    public static YakuCondition WinningGroup(GroupFilter filter) => Custom(w => filter.Matches(w.GroupAt(w.IncomingGroup)));
    public static YakuCondition Wait(WaitShape shape) => Custom(w => WaitOf(w) == shape);

    /// <summary>
    /// An arbitrary test. Omitted partial tests default to true: always correct,
    /// but such a condition cannot prune wildcard targets.
    /// </summary>
    public static YakuCondition Custom(Func<WinView, bool> test, Func<PartialView, bool> mayHold = null,
        Func<PartialView, bool> mayFail = null) => new DelegateCondition(test, mayHold, mayFail);

    internal static ulong Mask(Tiles tiles) {
      ulong mask = 0;
      foreach (var t in tiles) {
        if (t.IsWildcard || !t.IsValid) throw new ArgumentException($"Conditions describe natural tiles, not {t}", nameof(tiles));
        mask |= 1UL << CompactMeld.Index(t);
      }
      return mask;
    }

    private static WaitShape WaitOf(WinView w) {
      var g = w.GroupAt(w.IncomingGroup); int t = w.IncomingTile;
      return g.Kind switch {
        CompactMeldKind.Pair => WaitShape.Tanki,
        CompactMeldKind.Triplet or CompactMeldKind.Quad => WaitShape.Shanpon,
        CompactMeldKind.Single => WaitShape.Single,
        _ when t == g.TileIndex + 1 => WaitShape.Kanchan,
        _ when g.TileIndex % 9 == 0 && t == g.TileIndex + 2 || g.TileIndex % 9 == 6 && t == g.TileIndex => WaitShape.Penchan,
        _ => WaitShape.Ryanmen,
      };
    }
  }

  internal sealed class AllCondition(YakuCondition[] parts) : YakuCondition {
    internal readonly YakuCondition[] Parts = parts;
    public override bool Test(WinView win) {
      foreach (var p in Parts) if (!p.Test(win)) return false;
      return true;
    }
    public override bool MayHold(PartialView v) {
      foreach (var p in Parts) if (!p.MayHold(v)) return false;
      return true;
    }
    public override bool MayFail(PartialView v) {
      foreach (var p in Parts) if (p.MayFail(v)) return true;
      return false;
    }
  }

  internal sealed class AnyCondition(YakuCondition[] parts) : YakuCondition {
    internal readonly YakuCondition[] Parts = parts;
    public override bool Test(WinView win) {
      foreach (var p in Parts) if (p.Test(win)) return true;
      return false;
    }
    public override bool MayHold(PartialView v) {
      foreach (var p in Parts) if (p.MayHold(v)) return true;
      return false;
    }
    public override bool MayFail(PartialView v) {
      foreach (var p in Parts) if (!p.MayFail(v)) return false;
      return true;
    }
  }

  internal sealed class NotCondition(YakuCondition inner) : YakuCondition {
    internal readonly YakuCondition Inner = inner;
    public override bool Test(WinView win) => !Inner.Test(win);
    public override bool MayHold(PartialView v) => Inner.MayFail(v);
    public override bool MayFail(PartialView v) => Inner.MayHold(v);
  }

  // Context is identical for every interpretation, so its partial tests are exact.
  internal sealed class ContextCondition(Func<YakuView, bool> test) : YakuCondition {
    public override bool Test(WinView win) => test(win);
    public override bool MayHold(PartialView v) => test(v);
    public override bool MayFail(PartialView v) => !test(v);
  }

  internal sealed class DelegateCondition(Func<WinView, bool> test, Func<PartialView, bool> mayHold,
      Func<PartialView, bool> mayFail) : YakuCondition {
    public override bool Test(WinView win) => test(win);
    public override bool MayHold(PartialView v) => mayHold?.Invoke(v) ?? true;
    public override bool MayFail(PartialView v) => mayFail?.Invoke(v) ?? true;
  }

  // Completions only add tiles to Present.
  internal sealed class TileSetCondition(ulong mask, bool all) : YakuCondition {
    public override bool Test(WinView win) => all ? (win.Present & ~mask) == 0 : (win.Present & mask) != 0;
    public override bool MayHold(PartialView v) => all ? (v.Present & ~mask) == 0 : (v.Present & mask) != 0 || v.RemainingTiles > 0;
    public override bool MayFail(PartialView v) => all ? (v.Present & ~mask) != 0 || v.RemainingTiles > 0 : (v.Present & mask) == 0;
  }

  internal sealed class WinningTileCondition(ulong mask) : YakuCondition {
    public override bool Test(WinView win) => (mask & (1UL << win.IncomingTile)) != 0;
    public override bool MayHold(PartialView v) => v.IncomingTile < 0 || (mask & (1UL << v.IncomingTile)) != 0;
    public override bool MayFail(PartialView v) => v.IncomingTile < 0 || (mask & (1UL << v.IncomingTile)) == 0;
  }

  internal sealed class EveryCondition(GroupFilter filter) : YakuCondition {
    public override bool Test(WinView win) => filter.CountIn(win) == win.GroupCount;
    public override bool MayHold(PartialView v) {
      for (int n = 0; n < v.GroupCount; n++) if (!filter.MayMatch(v, v.GroupAt(n))) return false;
      return true;
    }
    public override bool MayFail(PartialView v) {
      if (v.RemainingTiles > 0) return true;
      for (int n = 0; n < v.GroupCount; n++) if (filter.MayMiss(v, v.GroupAt(n))) return true;
      return false;
    }
  }

  internal abstract class RangeCondition(int min, int max) : YakuCondition {
    protected abstract void Range(PartialView v, out int lo, out int hi);
    protected bool InRange(int n) => n >= min && n <= max;
    public override bool MayHold(PartialView v) {
      Range(v, out int lo, out int hi);
      return hi >= min && lo <= max;
    }
    public override bool MayFail(PartialView v) {
      Range(v, out int lo, out int hi);
      return lo < min || hi > max;
    }
  }

  internal sealed class GroupCountExpression(GroupFilter filter) : CountExpression {
    internal override YakuCondition Between(int min, int max) => new GroupCountCondition(filter, min, max);
  }

  internal sealed class GroupCountCondition(GroupFilter filter, int min, int max) : RangeCondition(min, max) {
    public override bool Test(WinView win) => InRange(filter.CountIn(win));
    protected override void Range(PartialView v, out int lo, out int hi) => filter.Range(v, out lo, out hi);
  }

  internal sealed class TileCountExpression(ulong mask) : CountExpression {
    internal override YakuCondition Between(int min, int max) => new TileCountCondition(mask, min, max);
  }

  internal sealed class TileCountCondition(ulong mask, int min, int max) : RangeCondition(min, max) {
    public override bool Test(WinView win) {
      int n = 0;
      for (int i = 0; i < 34; i++) if ((mask & (1UL << i)) != 0) n += win.TileCount(i);
      return InRange(n);
    }
    protected override void Range(PartialView v, out int lo, out int hi) {
      lo = 0; hi = v.RemainingTiles;
      for (int i = 0; i < 34; i++) if ((mask & (1UL << i)) != 0) { lo += v.MinCount(i); hi += v.PlacedCount(i); }
    }
  }
}
