using RabiRiichi.Communication;

namespace RabiRiichi.Event.InGame {
    [RabiIgnore]
    public class TerminateEvent : EventBase {
        public override string name => "terminate";

        public TerminateEvent(EventBase parent) : base(parent) { }
    }
}