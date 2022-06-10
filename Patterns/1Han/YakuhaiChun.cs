using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
    public class YakuhaiChun : Yakuhai {
        public YakuhaiChun(Base33332 base33332) : base(base33332) { }
        protected override Tile YakuTile => new("7z");
    }
}
