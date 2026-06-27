using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class NextGameEvent(ConcludeGameEvent parent) : EventBase(parent) {
    public override string name => "next_game";
    #region Response
    /// <summary> 轮数 </summary>
    [RabiBroadcast] public int nextRound;
    /// <summary> 局数 </summary>
    [RabiBroadcast] public int nextDealer;
    /// <summary> 本场 </summary>
    [RabiBroadcast] public int nextHonba;
    /// <summary> 立直棒 </summary>
    [RabiBroadcast] public int riichiStick;

    #endregion
  }
}