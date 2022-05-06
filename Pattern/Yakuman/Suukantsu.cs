using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Suukantsu : StdPattern {
        public Suukantsu(Base33332 base33332, Sankantsu sankantsu, Toitoi toitoi) {
            BaseOn(base33332);
            DependOn(sankantsu, toitoi);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.OfType<Kan>().Count() == 4) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }

        public override bool OnScoreTransfer(Player player, ScoreTransferList scoreTransfers) {
            var kans = player.hand.called.OfType<Kan>().ToArray();
            if (kans.Length != 4) {
                return false;
            }
            var lastGroup = kans[^1];
            if (lastGroup.KanSource != TileSource.DaiMinKan) {
                return false;
            }
            if (PaoUtil.TryGetPaoPlayer(lastGroup, out int paoPlayer)) {
                return ApplyPao(player, paoPlayer, 32000, scoreTransfers);
            }
            return false;
        }
    }
}
