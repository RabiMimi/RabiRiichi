using RabiRiichi.Core;

namespace RabiRiichi.Pattern {
    public class YakuhaiHaku : 役牌 {
        public YakuhaiHaku(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("5z");
    }
}
