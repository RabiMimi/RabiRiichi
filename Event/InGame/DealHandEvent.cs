using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";

        public DealHandEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}
