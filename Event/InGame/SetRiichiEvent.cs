using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class SetRiichiEvent : BroadcastPlayerEvent {
        public override string name => "set_riichi";

        #region Request
        public GameTile riichiTile;
        #endregion

        public SetRiichiEvent(Game game, int playerId, GameTile riichiTile) : base(game, playerId) {
            this.riichiTile = riichiTile;
        }
    }
}