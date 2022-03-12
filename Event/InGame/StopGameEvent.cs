using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class StopGameEvent : EventBase {
        public override string name => "stop_game";

        public StopGameEvent(Game game) : base(game) { }
    }
}
