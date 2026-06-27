using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
  [RabiIgnore]
  public class TerminateEvent(EventBase parent) : EventBase(parent) {
    public override string name => "terminate";
  }
}