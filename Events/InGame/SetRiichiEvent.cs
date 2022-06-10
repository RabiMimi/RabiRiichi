using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Events.InGame {
    public class SetRiichiEvent : BroadcastPlayerEvent {
        public override string name => "set_riichi";

        #region Request
        [RabiBroadcast] public GameTile riichiTile;
        [RabiBroadcast] public bool wRiichi;
        #endregion

        public SetRiichiEvent(EventBase parent, int playerId, GameTile riichiTile, bool wRiichi) : base(parent, playerId) {
            this.riichiTile = riichiTile;
            this.wRiichi = wRiichi;
        }
    }
}