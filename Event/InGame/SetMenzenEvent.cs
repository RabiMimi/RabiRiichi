using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class SetMenzenEvent : BroadcastPlayerEvent {
        public override string name => "increase_jun";

        #region Response
        public bool menzen;
        #endregion

        public SetMenzenEvent(Game game, int playerId, bool menzen) : base(game, playerId) {
            this.menzen = menzen;
        }
    }
}