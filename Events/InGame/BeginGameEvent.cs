using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class BeginGameEvent(EventBase parent, int round, int dealer, int honba, int riichiStick) : EventBase(parent) {
    public override string name => "begin_game";

    #region Request
    /// <summary> 轮数 </summary>
    [RabiBroadcast] public int round = round;
    /// <summary> 局数 </summary>
    [RabiBroadcast] public int dealer = dealer;
    /// <summary> 本场 </summary>
    [RabiBroadcast] public int honba = honba;
    /// <summary> 立直棒数量 </summary>
    [RabiBroadcast] public int riichiStick = riichiStick;
    #endregion

    #region Response
    /// <summary> 牌山总牌数 </summary>
    [RabiBroadcast] public int remainingTiles;

    #endregion Response
  }
}