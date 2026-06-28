using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  public class ApplyScoreEvent(EventBase parent, ScoreTransferList scoreChange) : EventBase(parent) {
    public override string name => "apply_score";

    #region Request
    [RabiBroadcast] public readonly ScoreTransferList scoreChange = scoreChange;

    #endregion
  }
}