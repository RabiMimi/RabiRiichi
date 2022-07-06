using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class IncreaseJunEvent : BroadcastPlayerEvent {
        public override string name => "increase_jun";

        #region Response
        [RabiBroadcast] public int increasedJun;
        #endregion

        public IncreaseJunEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}