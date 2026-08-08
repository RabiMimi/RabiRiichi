using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  /// <summary> 大車輪：22334455667788筒。役满，门前清限定 </summary>
  public class Daisharin : DaisharinLike {
    protected override TileSuit MatchSuit => TileSuit.P;

    public Daisharin(Base72 base72, Chiitoitsu chiitoitsu, Tanyao tanyao, Chinitsu chinitsu) {
      BaseOn(base72);
      DependOn(chiitoitsu);
      After(tanyao, chinitsu);
    }
  }
}
