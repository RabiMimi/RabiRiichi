using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Daisangen : StdPattern {
        public Daisangen(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool flag = groups
                .Where(gr => gr.First.tile.IsSangen && gr is not Jantou)
                .GroupBy(gr => gr.First.tile)
                .Count() == 3;
            if (flag) {
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }

        public override bool ResolvePao(Player player, ScoreTransferList scoreTransfers) {
            var sangenGroups = player.hand.called
                .Where(gr => gr.First.tile.IsSangen && gr is not Jantou)
                .ToArray();
            if (sangenGroups.GroupBy(gr => gr.First.tile).Count() != 3) {
                return false;
            }
            var lastGroup = sangenGroups[^1];
            if (PaoUtil.TryGetPaoPlayer(lastGroup, out int paoPlayer)) {
                return ApplyPao(player, paoPlayer, scoreTransfers);
            }
            return false;
        }
    }
}
