using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class EndGameEvent : EventBase {
        public override string name => "end_game";
        public EndGameEvent(Game game) : base(game) { }
    }
}