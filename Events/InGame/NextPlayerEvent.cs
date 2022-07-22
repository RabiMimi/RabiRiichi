using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
    public class NextPlayerEvent : PlayerEvent {
        public override string name => "next_player";

        #region Response
        [RabiBroadcast] public int nextPlayerId;
        #endregion

        public NextPlayerEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}