using RabiRiichi.Core;

namespace RabiRiichi.Pattern {
    public class 役牌发 : 役牌 {
        public 役牌发(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("6z");
    }
}
