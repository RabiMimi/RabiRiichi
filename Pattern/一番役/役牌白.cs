using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 役牌白 : 役牌 {
        public 役牌白(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("5z");
    }
}
