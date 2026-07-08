using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
  public class TenpaiInfo {
    public Tile winningTile;
    public int han;
    /// <summary> 记作役的番数（不含宝牌），用于番缚判定 </summary>
    public int yaku;
    public int fu;
    public int yakuman;
    public long points;
  }

  public class DiscardCandidate {
    public GameTile tile;
    public List<TenpaiInfo> tenpaiInfos;
  }

  public class PlayTileAction : ChooseTileAction {
    public override string name => "play_tile";
    public readonly GameTile defaultTile;
    public List<DiscardCandidate> candidates;

    public PlayTileAction(int playerId, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(playerId, tiles, priorityDelta) {
      this.defaultTile = defaultTile;
      int index = tiles.IndexOf(defaultTile);
      if (ValidateResponse(index)) {
        response = index;
      }
      candidates = [];
    }

    public PlayTileAction(Player player, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(player.id, tiles, priorityDelta) {
      this.defaultTile = defaultTile;
      int index = tiles.IndexOf(defaultTile);
      if (ValidateResponse(index)) {
        response = index;
      }
      candidates = ComputeDiscardCandidates(player, tiles, defaultTile);
    }

    public static List<DiscardCandidate> ComputeDiscardCandidates(
        Player player,
        List<GameTile> discardOptions,
        GameTile incoming) {
      var candidates = new List<DiscardCandidate>();
      var hand = player.hand;
      var patternResolver = player.game.Get<PatternResolver>();

      var originalFreeTiles = hand.freeTiles.ToList();
      var originalPendingTile = hand.pendingTile;

      var all14Tiles = new List<GameTile>(originalFreeTiles);
      if (incoming != null) {
        all14Tiles.Add(incoming);
      }

      foreach (var optionTile in discardOptions) {
        var tempFreeTiles = new List<GameTile>(all14Tiles);
        tempFreeTiles.Remove(optionTile);

        hand.freeTiles = tempFreeTiles;
        hand.pendingTile = null;

        int shanten = patternResolver.ResolveShanten(hand, null, out var machihai, 0);

        var candidate = new DiscardCandidate {
          tile = optionTile,
          tenpaiInfos = []
        };

        if (shanten == 0) {
          foreach (var winTile in machihai) {
            candidate.tenpaiInfos.Add(
                ComputeTenpaiInfo(patternResolver, hand, winTile));
          }
        }

        candidates.Add(candidate);
      }

      hand.freeTiles = originalFreeTiles;
      hand.pendingTile = originalPendingTile;

      return candidates;
    }

    /// <summary>
    /// Computes the guaranteed-minimum score a wait can achieve, for display in
    /// the tenpai preview. The win is scored as a ron (so tsumo-only value like
    /// suuankou or menzen tsumo is not assumed) and accidental yaku
    /// (PatternMask.Luck: ippatsu, haitei/houtei, rinshan, chankan) are excluded.
    /// </summary>
    public static TenpaiInfo ComputeTenpaiInfo(
        PatternResolver patternResolver, Hand hand, Tile winTile) {
      var winGameTile = new GameTile(winTile, 0) {
        discardInfo = new DiscardInfo(null, DiscardReason.Draw, 0),
        source = TileSource.Discard,
      };
      var scores = patternResolver.ResolveMaxScore(
          hand, winGameTile, PatternMask.Regular | PatternMask.Bonus);

      return new TenpaiInfo {
        winningTile = winTile,
        han = scores?.result?.han ?? 0,
        yaku = scores?.result?.yaku ?? 0,
        fu = scores?.result?.fu ?? 0,
        yakuman = scores?.result?.yakuman ?? 0,
        points = scores?.result?.BaseScore ?? 0,
      };
    }
  }
}