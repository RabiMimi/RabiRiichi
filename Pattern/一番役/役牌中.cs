using RabiRiichi.Riichi;

namespace RabiRiichi.Pattern {
    public class 役牌中 : 役牌 {
        public 役牌中(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("7z");
    }
}
