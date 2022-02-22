using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    public class StopGameEvent : EventBase {
        public StopGameEvent(Game game) : base(game) { }
    }
}
