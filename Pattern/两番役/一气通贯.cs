using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 一气通贯 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            var grs = groups.Where(gr => gr is not Jantou).OrderBy(gr => gr[0]).ToList();
            for (int i = 0; i < grs.Count; i++) {
                var checkList = grs.Where((gr, index) => index != i);
                // 清一色
                if (!checkList.All(gr => gr.Suit == checkList.First().Suit))
                    return false;
                // 一气通贯
                if (checkList.All((gr, index) => gr[0].tile.Num == index * 3 + 1)) {
                    scorings.Add(new Scoring(ScoringType.Han, hand.menzen ? 2 : 1, this));
                    return true;
                }
            }
            return false;
        }
    }
}