using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class NextPlayerEvent : BroadcastPlayerEvent {
        public override string name => "next_player";

        #region Response
        [RabiBroadcast] public int nextPlayerId;
        #endregion

        public NextPlayerEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}