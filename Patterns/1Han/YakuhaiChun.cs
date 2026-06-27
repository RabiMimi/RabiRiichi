using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  public class YakuhaiChun(Base33332 base33332) : Yakuhai(base33332) {
    protected override Tile YakuTile => new("7z");
  }
}