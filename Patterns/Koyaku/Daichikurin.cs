using RabiRiichi.Core;

namespace RabiRiichi.Patterns {
  /// <summary> 大竹林：22334455667788索。役满，门前清限定 </summary>
  public class Daichikurin : DaisharinLike {
    protected override TileSuit MatchSuit => TileSuit.S;

    public Daichikurin(Base72 base72, Chiitoitsu chiitoitsu, Tanyao tanyao, Chinitsu chinitsu) {
      BaseOn(base72);
      DependOn(chiitoitsu);
      After(tanyao, chinitsu);
    }
  }
}
