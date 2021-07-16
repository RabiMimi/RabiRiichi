﻿using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Pinfu : StdPattern {
        private Type[] mBasePatterns = new Type[] { typeof(Base33332) };
        public override Type[] basePatterns => mBasePatterns;

        private Type[] mDependOnPatterns = new Type[0];
        public override Type[] dependOnPatterns => mDependOnPatterns;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen || groups.Any(gr => gr.IsKan || gr.IsKou))
                return false;
            var gr = groups.Find(gr => gr.Contains(incoming));
            if (!gr.IsShun)
                return false;
            if (gr.Any(tile => tile.tile.Is19 && tile != incoming))
                return false;
            gr.Sort();
            if (incoming == gr[1])
                return false;
            scorings.Add(new Scoring {
                Type = ScoringType.Han,
                Val = 1,
                Source = this
            });
            return true;
        }
    }
}
