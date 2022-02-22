using System;
using System.Collections.Generic;
using System.Text;
using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern
{
    public class 役牌中 : 役牌
    {
        protected override Tile YakuTile => new Tile("7z");
    }
}
