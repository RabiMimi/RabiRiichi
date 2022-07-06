using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class NextPlayerEvent : BroadcastPlayerEvent {
        public override string name => "next_player";

        #region Response
        [RabiBroadcast] public int nextPlayerId;
        #endregion

        public NextPlayerEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}