using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class IncreaseJunEvent : BroadcastPlayerEvent {
        public override string name => "increase_jun";

        #region Response
        public int increasedJun;
        #endregion

        public IncreaseJunEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}