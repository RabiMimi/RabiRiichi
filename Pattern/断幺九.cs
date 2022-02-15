using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern
{
    public class 断幺九 : StdPattern
    {
        public override Type[] dependOnPatterns => AllBasePatterns;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings)
        {
            if (hand.allTiles.Any(tile => tile.tile.Is19Z) || incoming.tile.Is19Z)
                return false;
            scorings.Add(new Scoring
            {
                Type = ScoringType.Han,
                Val = 1,
                Source = this
            });
            return true;
        }
    }
}
