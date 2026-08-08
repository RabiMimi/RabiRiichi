using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  public class Daisuushii : StdPattern {
    public Daisuushii(Base33332 base33332, Sanfonkou sanfonkou) {
      BaseOn(base33332);
      // 大四喜的牌型同时满足三風刻
      After(sanfonkou);
    }

    public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
      bool flag = groups
          .Where(gr => gr.First.tile.IsWind && gr is not Jantou)
          .GroupBy(gr => gr.First.tile)
          .Count() == 4;
      if (flag) {
        scores.Remove(afterPatterns);
        scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
        return true;
      }
      return false;
    }

    public override bool OnScoreTransfer(Player player, ScoreTransferList scoreTransfers) {
      var windGroups = player.hand.called
          .Where(gr => gr.First.tile.IsWind && gr is not Jantou)
          .ToArray();
      if (windGroups.GroupBy(gr => gr.First.tile).Count() != 4) {
        return false;
      }
      var lastGroup = windGroups[^1];
      return PaoUtil.TryGetPaoPlayer(lastGroup, out int paoPlayer) && ApplyPao(player, paoPlayer, 64000, scoreTransfers);
    }
  }
}