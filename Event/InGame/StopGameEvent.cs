using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class StopGameEvent : EventBase {
        public StopGameEvent(Game game) : base(game) { }
    }
}
