using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
  public class TenpaiInfo {
    internal TenpaiInfo Copy() => (TenpaiInfo)MemberwiseClone();
    public Tile winningTile;
    public int han;
    /// <summary> 记作役的番数（不含宝牌），用于番缚判定 </summary>
    public int yaku;
    public int fu;
    /// <summary> 能满足番缚的役满数 </summary>
    public int yakuman;
    /// <summary> 奖励役满，计入点数但不满足番缚 </summary>
    public int bonusYakuman;
    public long points;
    /// <summary> 荣和/自摸中较高者的番数，含宝牌，役满按 13 番计 </summary>
    public int maxHan;
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

      var all14Tiles = new List<GameTile>(hand.freeTiles);
      if (incoming != null) {
        all14Tiles.Add(incoming);
      }

      var cache = new Dictionary<Tile, List<TenpaiInfo>>();
      bool canShare = patternResolver.CanShareScoring;
      foreach (var optionTile in discardOptions) {
        if (canShare && cache.TryGetValue(optionTile.tile, out var cached)) {
          candidates.Add(new DiscardCandidate { tile = optionTile, tenpaiInfos = cached.Select(info => info.Copy()).ToList() });
          continue;
        }
        var tempFreeTiles = new List<GameTile>(all14Tiles);
        tempFreeTiles.Remove(optionTile);

        var analysisHand = hand.WithFreeTiles(tempFreeTiles);
        int shanten = patternResolver.ResolveShanten(analysisHand, null, out var machihai, 0);

        var candidate = new DiscardCandidate {
          tile = optionTile,
          tenpaiInfos = []
        };

        if (shanten == 0) {
          candidate.tenpaiInfos = ComputeTenpaiInfos(patternResolver, analysisHand, machihai);
        }

        candidates.Add(candidate);
        cache[optionTile.tile] = candidate.tenpaiInfos;
      }

      return candidates;
    }

    /// <summary>
    /// 听牌预览的番数区间：下限按荣和算，上限取荣和/自摸中较高者。
    /// 两者都排除运气役（PatternMask.Luck）。
    /// </summary>
    public static TenpaiInfo ComputeTenpaiInfo(
        PatternResolver patternResolver, Hand hand, Tile winTile) =>
        ComputeTenpaiInfos(patternResolver, hand, [winTile])[0];

    public static List<TenpaiInfo> ComputeTenpaiInfos(PatternResolver resolver, Hand hand, Tiles winningTiles) {
      var previews = resolver.ResolveScorePreviews(hand, winningTiles, PatternMask.All, PatternMask.Luck);
      return winningTiles.Select(tile => FromScores(tile, previews[tile].Ron?.result, previews[tile].Tsumo?.result)).ToList();
    }

    private static TenpaiInfo FromScores(Tile winTile, ScoreCalcResult ron, ScoreCalcResult tsumo) {
      // 自摸要靠自己摸上来，只有荣和算保底。
      var ceiling = Better(ron, tsumo);

      return new TenpaiInfo {
        winningTile = winTile,
        han = ron?.han ?? 0,
        yaku = ron?.yaku ?? 0,
        fu = ron?.fu ?? 0,
        yakuman = ron?.yakuman ?? 0,
        bonusYakuman = ron?.bonusYakuman ?? 0,
        points = ron?.BaseScore ?? 0,
        maxHan = EffectiveHan(ceiling),
      };
    }

    /// <summary>
    /// 役满按每个 13 番计，使番数单值即可表达"有役满可能"。
    /// 与 BaseScore 保持一致：青天井下役满与普通番累加且无上限，
    /// 否则役满取代普通番。
    /// </summary>
    private static int EffectiveHan(ScoreCalcResult result) {
      if (result == null) return 0;
      // 上限按点数算，奖励役满也计入
      int yakumanHan = result.TotalYakuman * ScoreCalcResult.KAZOE_YAKUMAN_HAN;
      if (!result.scoringOption.HasAnyFlag(ScoringOption.Yakuman)) {
        return result.han + yakumanHan;
      }
      return result.TotalYakuman > 0 ? yakumanHan : result.han;
    }

    private static ScoreCalcResult Better(ScoreCalcResult a, ScoreCalcResult b) {
      if (a == null) return b;
      if (b == null) return a;
      return b.CompareTo(a) > 0 ? b : a;
    }
  }
}
