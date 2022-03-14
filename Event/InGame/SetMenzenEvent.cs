using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class SetMenzenEvent : BroadcastPlayerEvent {
        public override string name => "set_menzen";

        #region Request
        public bool menzen;
        #endregion

        public SetMenzenEvent(Game game, int playerId, bool menzen) : base(game, playerId) {
            this.menzen = menzen;
        }
    }
}