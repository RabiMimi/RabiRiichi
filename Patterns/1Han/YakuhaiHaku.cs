﻿using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
    public class YakuhaiHaku : Yakuhai {
        public YakuhaiHaku(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("5z");
    }
}
