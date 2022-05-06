using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Daisuushii : StdPattern {
        public Daisuushii(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups
                .Where(gr => gr.First.tile.IsWind && gr is not Jantou)
                .GroupBy(gr => gr.First.tile)
                .Count() == 4;
            if (flag) {
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
            if (PaoUtil.TryGetPaoPlayer(lastGroup, out int paoPlayer)) {
                return ApplyPao(player, paoPlayer, 32000, scoreTransfers);
            }
            return false;
        }
    }
}
