using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern
{
    public class 一杯口 : StdPattern
    {
        public override Type[] dependOnPatterns => Only33332;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings)
        {
            if(!hand.menzen)
                return false;
            var gr = groups.Where(tiles => tiles.IsShun).ToList();
            if (gr.Count < 2)
                return false;
            for (int i = 0; i < gr.Count; i++)
            {
                for (int j = 0; j < gr.Count; j++)
                {
                    if (i != j)
                    {
                        if (gr[i].IsSame(gr[j]))
                        {
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
            }
            return false;
        }
    }
}
