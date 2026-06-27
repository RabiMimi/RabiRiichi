using RabiRiichi.Actions;
using RabiRiichi.Communication;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
  [RabiIgnore]
  public class WaitPlayerActionEvent : EventBase {
    public override string name => "wait_player_action";

    #region Request
    public readonly MultiPlayerInquiry inquiry;
    public readonly MultiEventBuilder eventBuilder = new();
    public TimeSpan timeout = TimeSpan.Zero;
    #endregion

    #region Response
    public readonly List<EventBase> responseEvents = [];
    #endregion

    public WaitPlayerActionEvent(EventBase parent) : base(parent) {
      inquiry = new MultiPlayerInquiry(game);
    }
  }

  [RabiPrivate]
  public class EndInquiryEvent(EventBase parent, int playerId) : PrivatePlayerEvent(parent, playerId) {
    public override string name => "end_inquiry";
  }
}