using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class SetRiichiEvent : BroadcastPlayerEvent {
        public override string name => "set_riichi";

        #region Request
        public bool riichi;
        #endregion

        public SetRiichiEvent(Game game, int playerId, bool riichi) : base(game, playerId) {
            this.riichi = riichi;
        }
    }
}