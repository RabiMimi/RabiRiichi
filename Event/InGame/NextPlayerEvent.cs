using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class NextPlayerEvent : BroadcastPlayerEvent {
        public override string name => "next_player";

        #region Response
        [RabiBroadcast] public int nextPlayerId;
        #endregion

        public NextPlayerEvent(Game game, int playerId) : base(game, playerId) { }
    }
}