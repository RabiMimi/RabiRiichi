using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  public class YakuhaiHatsu(Base33332 base33332) : Yakuhai(base33332) {
    protected override Tile YakuTile => new("6z");
  }
}