using RabiRiichi.Core;

namespace RabiRiichi.Pattern {
    public class YakuhaiHatsu : Yakuhai {
        public YakuhaiHatsu(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("6z");
    }
}
