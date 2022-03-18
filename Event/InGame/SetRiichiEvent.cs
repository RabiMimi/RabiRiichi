using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class SetRiichiEvent : BroadcastPlayerEvent {
        public override string name => "set_riichi";

        #region Request
        [RabiBroadcast] public GameTile riichiTile;
        [RabiBroadcast] public bool wRiichi;
        #endregion

        public SetRiichiEvent(Game game, int playerId, GameTile riichiTile, bool wRiichi) : base(game, playerId) {
            this.riichiTile = riichiTile;
            this.wRiichi = wRiichi;
        }
    }
}