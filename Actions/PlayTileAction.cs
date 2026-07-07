using RabiRiichi.Core;
using RabiRiichi.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
  public class TenpaiInfo {
    public Tile winningTile;
    public int remainingCount;
    public int han;
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
            var winGameTile = new GameTile(winTile, 0);
            var scores = patternResolver.ResolveMaxScore(hand, winGameTile, PatternMask.All);

            int remaining = CountRemainingTiles(player, winTile);

            var tenpaiInfo = new TenpaiInfo {
              winningTile = winTile,
              remainingCount = remaining,
              han = scores?.result?.han ?? 0,
              fu = scores?.result?.fu ?? 0,
              yakuman = scores?.result?.yakuman ?? 0,
              points = scores?.result?.BaseScore ?? 0
            };
            candidate.tenpaiInfos.Add(tenpaiInfo);
          }
        }

        candidates.Add(candidate);
      }

      hand.freeTiles = originalFreeTiles;
      hand.pendingTile = originalPendingTile;

      return candidates;
    }

    public static int CountRemainingTiles(Player player, Tile winTile) {
      var game = player.game;
      var target = winTile.WithoutDora;
      int count = 0;

      foreach (var tile in player.hand.freeTiles) {
        if (tile.tile.WithoutDora == target) {
          count++;
        }
      }
      if (player.hand.pendingTile != null && player.hand.pendingTile.tile.WithoutDora == target) {
        count++;
      }

      foreach (var p in game.players) {
        foreach (var meld in p.hand.called) {
          foreach (var tile in meld) {
            if (tile.tile.WithoutDora == target) {
              count++;
            }
          }
        }
      }

      foreach (var p in game.players) {
        foreach (var tile in p.hand.discarded) {
          if (tile.tile.WithoutDora == target) {
            count++;
          }
        }
      }

      foreach (var dora in game.wall.doras.Take(game.wall.revealedDoraCount)) {
        if (dora.tile.WithoutDora == target) {
          count++;
        }
      }

      return Math.Max(0, 4 - count);
    }
  }
}