using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
    [RabiIgnore]
    public class TerminateEvent : EventBase {
        public override string name => "terminate";

        public TerminateEvent(EventBase parent) : base(parent) { }
    }
}