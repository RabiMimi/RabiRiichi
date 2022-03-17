using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";

        public DealHandEvent(Game game, int playerId) : base(game, playerId) { }
    }
}
