using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  public class YakuhaiHaku(Base33332 base33332) : Yakuhai(base33332) {
    protected override Tile YakuTile => new("5z");
  }
}