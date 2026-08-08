using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  /// <summary> 大数隣：22334455667788万。役满，门前清限定 </summary>
  public class Daisuurin : DaisharinLike {
    protected override TileSuit MatchSuit => TileSuit.M;

    public Daisuurin(Base72 base72, Chiitoitsu chiitoitsu, Tanyao tanyao, Chinitsu chinitsu) {
      BaseOn(base72);
      DependOn(chiitoitsu);
      After(tanyao, chinitsu);
    }
  }
}
