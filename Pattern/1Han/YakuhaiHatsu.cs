using RabiRiichi.Core;

namespace RabiRiichi.Pattern {
    public class YakuhaiHatsu : 役牌 {
        public YakuhaiHatsu(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("6z");
    }
}
