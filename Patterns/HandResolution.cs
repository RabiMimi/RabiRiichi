using RabiRiichi.Core;
using RabiRiichi.Patterns.Compiled;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public sealed record ResolvedTile(GameTile Physical, Tile Effective);
  public sealed record ResolvedGroup(CompactMeldKind Kind, IReadOnlyList<ResolvedTile> Tiles);

  /// <summary>A scoring witness. Physical tile values and histories are never rewritten.</summary>
  public sealed class HandResolution {
    public IReadOnlyList<ResolvedGroup> Groups { get; }
    public ResolvedTile Incoming { get; }
    /// <summary>Diagnostics for bounded target search, independent of wildcard permutations.</summary>
    public long VisitedTargets { get; internal set; }
    public long EvaluatedInterpretations { get; internal set; }

    internal HandResolution(Hand hand, GameTile incoming, IReadOnlyList<CompactMeld> groups,
        int incomingGroup, int incomingIndex) {
      var natural = Enumerable.Range(0, 34).Select(_ => new Queue<GameTile>()).ToArray();
      var wild = new Queue<GameTile>();
      foreach (var tile in hand.freeTiles) {
        if (tile.tile.IsWildcard) wild.Enqueue(tile); else natural[CompactMeld.Index(tile.tile)].Enqueue(tile);
      }
      var output = new List<ResolvedGroup>();
      bool placed = false;
      for (int n = 0; n < groups.Count; n++) {
        var group = groups[n]; var tiles = new List<ResolvedTile>();
        for (int pos = 0; pos < group.Count; pos++) {
          int i = group.TileIndex + (group.Kind == CompactMeldKind.Sequence ? pos : 0);
          GameTile physical;
          if (group.IsCalled) physical = hand.called[n][pos];
          else if (n == incomingGroup && i == incomingIndex && !placed) { physical = incoming; placed = true; }
          else physical = natural[i].Count > 0 ? natural[i].Dequeue() : wild.Dequeue();
          var resolved = new ResolvedTile(physical, physical.tile.IsWildcard ? CompactMeld.TileAt(i) : physical.tile);
          if (physical == incoming) Incoming = resolved;
          tiles.Add(resolved);
        }
        output.Add(new(group.Kind, tiles.AsReadOnly()));
      }
      Groups = output.AsReadOnly();
    }
  }
}
