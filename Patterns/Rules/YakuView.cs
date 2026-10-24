using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns.Compiled;
using System;

namespace RabiRiichi.Patterns {
  /// <summary>
  /// What a <see cref="YakuCondition"/> may read about the win itself. It never
  /// exposes the winning tile's identity, so a context test means the same for a
  /// complete interpretation and for a partial wildcard target.
  /// </summary>
  public abstract class YakuView {
    /// <summary>Physical hand state (riichi, menzen, seat). Its tiles may be wildcards.</summary>
    public Hand Hand { get; private set; }
    public bool IsTsumo { get; private set; }
    public TileSource Source { get; private set; }
    /// <summary>Why the winning tile left its owner, or null for a tsumo.</summary>
    public DiscardReason? DiscardReason { get; private set; }
    public bool IsRiichiDeclaration { get; private set; }
    public bool Closed => Hand.menzen;

    internal void SetContext(Hand hand, GameTile incoming) {
      Hand = hand; IsTsumo = incoming.IsTsumo; Source = incoming.source;
      DiscardReason = incoming.discardInfo?.reason;
      IsRiichiDeclaration = incoming.discardInfo?.isRiichiDeclaration == true;
    }

    /// <summary>Bits over the 34 tile indices covered by a group.</summary>
    public static ulong TileMask(CompactMeld g) => g.Kind == CompactMeldKind.Sequence ? 7UL << g.TileIndex : 1UL << g.TileIndex;
  }

  /// <summary>A complete interpretation, with wildcards replaced by effective tiles.</summary>
  public sealed class WinView : YakuView {
    private readonly CompactMeld[] groups = new CompactMeld[Game.HAND_SIZE + 1];
    private readonly byte[] counts = new byte[34];
    internal WinView() { }
    public int GroupCount { get; private set; }
    /// <summary>Called melds included. A group completed by a discarded tile is open.</summary>
    public CompactMeld GroupAt(int index) => index < GroupCount ? groups[index] : throw new ArgumentOutOfRangeException(nameof(index));
    public int IncomingGroup { get; private set; }
    /// <summary>Effective tile index (0..33) of the winning tile.</summary>
    public int IncomingTile { get; private set; }
    /// <summary>Copies of a tile index across every group.</summary>
    public int TileCount(int tile) => counts[tile];
    /// <summary>Bits over tile indices with at least one copy.</summary>
    public ulong Present { get; private set; }

    internal void Set(Hand hand, GameTile incoming, ReadOnlySpan<CompactMeld> groups, ReadOnlySpan<byte> counts,
        int incomingGroup, int incomingTile) {
      SetContext(hand, incoming);
      GroupCount = groups.Length; IncomingGroup = incomingGroup; IncomingTile = incomingTile;
      for (int n = 0; n < groups.Length; n++) {
        var g = groups[n];
        this.groups[n] = g.IsClose && n == incomingGroup && !incoming.IsTsumo ? g with { IsClose = false } : g;
      }
      ulong present = 0;
      for (int i = 0; i < 34; i++) {
        this.counts[i] = counts[i];
        if (counts[i] != 0) present |= 1UL << i;
      }
      Present = present;
    }
  }

  /// <summary>
  /// A partial wildcard target. Every completion keeps the placed groups, adds
  /// only free closed groups within the remaining capacity, and contains every
  /// tile in <see cref="Present"/>.
  /// </summary>
  public sealed class PartialView : YakuView {
    // Borrowed from the search; Set runs before every use.
    private CompactMeld[] groups = [];
    private byte[] placed = [], locked = [];
    private int[] required = [], natural = [];
    internal PartialView() { }
    public int GroupCount { get; private set; }
    /// <summary>Under ron, any one closed free group may still become the open winning group.</summary>
    public CompactMeld GroupAt(int index) => index < GroupCount ? groups[index] : throw new ArgumentOutOfRangeException(nameof(index));
    /// <summary>Free sequences or triplets still to place.</summary>
    public int RemainingMelds { get; private set; }
    public int RemainingPairs { get; private set; }
    public int RemainingSingles { get; private set; }
    public int RemainingTiles { get; private set; }
    /// <summary>The natural winning tile index, or -1 when it is a wildcard or varies across a batch.</summary>
    public int IncomingTile { get; private set; }
    /// <summary>Bits over tile indices every completion contains.</summary>
    public ulong Present { get; private set; }
    /// <summary>Copies of a tile index in the placed groups.</summary>
    public int PlacedCount(int tile) => placed[tile];
    /// <summary>Copies of a tile index every completion contains.</summary>
    public int MinCount(int tile) => Math.Max(placed[tile],
        Math.Max(required[tile], natural[tile] + (IncomingTile == tile ? 1 : 0)) + locked[tile]);

    /// <summary>Upper bound on future groups of the given kinds.</summary>
    internal int FutureGroups(GroupKinds kinds) =>
        ((kinds & (GroupKinds.Sequence | GroupKinds.Triplet)) != 0 ? RemainingMelds : 0)
        + ((kinds & GroupKinds.Pair) != 0 ? RemainingPairs : 0)
        + ((kinds & GroupKinds.Single) != 0 ? RemainingSingles : 0);

    internal void Set(CompactMeld[] groups, int count, byte[] placed, byte[] locked, int[] required, int[] natural,
        int incomingTile, ulong present) {
      this.groups = groups; GroupCount = count;
      this.placed = placed; this.locked = locked; this.required = required; this.natural = natural;
      IncomingTile = incomingTile; Present = present;
    }

    internal void SetRemaining(int melds, int pairs, int singles, int tiles) {
      RemainingMelds = melds; RemainingPairs = pairs; RemainingSingles = singles; RemainingTiles = tiles;
    }
  }
}
