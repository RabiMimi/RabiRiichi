using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabiRiichi.Pattern {
    public class 役牌发 : 役牌 {
        protected override Tile YakuTile => new("6z");
    }
}
