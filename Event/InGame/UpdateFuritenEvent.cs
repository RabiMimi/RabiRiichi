using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class UpdateFuritenEvent : BroadcastPlayerEvent {
        public override string name => "update_furiten";

        public UpdateFuritenEvent(Game game, int playerId) : base(game, playerId) { }
    }
}