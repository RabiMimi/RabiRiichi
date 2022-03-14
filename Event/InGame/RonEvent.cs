using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class RonEvent : BroadcastPlayerEvent {
        public override string name => "ron";
        #region Request
        [RabiBroadcast] public readonly GameTile incoming;
        #endregion

        public RonEvent(Game game, int playerId, GameTile incoming) : base(game, playerId) {
            this.incoming = incoming;
        }
    }
}