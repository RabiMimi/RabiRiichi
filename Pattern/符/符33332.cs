using RabiRiichi.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 符33332 : StdPattern {
        public 符33332(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            var baseFu = 20;

            // 面子带来的符数
            baseFu += groups.Sum(gr => CalculateMenlikeFu(gr));

            // 雀头带来的符数
            Tile jantouTile = groups.Find(gr => gr is Jantou).First.tile;
            baseFu += hand.game.IsYaku(jantouTile) ? 2 : 0;
            baseFu += Tile.From(hand.player.Wind).IsSame(jantouTile) ? 2 : 0;

            // 听牌型带来的符数
            baseFu += CalculateTenpaiFu(groups.Find(gr => gr.Contains(incoming)), incoming);

            // 和了牌带来的符数
            baseFu += CalculateAgariFu(hand, incoming);

            // 符数切上
            baseFu = Math.Max(baseFu, 30);
            baseFu = (baseFu + 9) / 10 * 10;
            scores.Add(new Scoring(ScoringType.Fu, baseFu, this));
            return true;
        }

        private static int CalculateMenlikeFu(MenLike group) =>
            group switch {

                Kou kou when kou.IsClose && kou.First.tile.Is19Z => 8,
                Kou kou when kou.IsClose && !kou.First.tile.Is19Z => 4,
                Kou kou when !kou.IsClose && kou.First.tile.Is19Z => 4,
                Kou kou when !kou.IsClose && !kou.First.tile.Is19Z => 2,

                Kan kan when kan.IsClose && kan.First.tile.Is19Z => 32,
                Kan kan when kan.IsClose && !kan.First.tile.Is19Z => 16,
                Kan kan when !kan.IsClose && kan.First.tile.Is19Z => 16,
                Kan kan when !kan.IsClose && !kan.First.tile.Is19Z => 8,

                _ => 0,
            };

        private static int CalculateTenpaiFu(MenLike group, GameTile incoming) {
            // 双碰
            if (group is Kou)
                return 0;
            // 边张
            if (group.Any(tile => tile.tile.Is19Z && tile != incoming))
                return 2;
            // 嵌张
            group.Sort();
            if (incoming == group[1])
                return 2;
            // 单骑
            if (group is Jantou)
                return 2;
            // 两面
            return 0;
        }

        private static int CalculateAgariFu(Hand hand, GameTile incoming) {
            if (incoming.IsTsumo)
                return 2;
            else if (hand.menzen)
                return 10;
            else
                return 0;
        }
    }
}
