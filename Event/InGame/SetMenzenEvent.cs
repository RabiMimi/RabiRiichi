namespace RabiRiichi.Event.InGame {
    public class SetMenzenEvent : BroadcastPlayerEvent {
        public override string name => "set_menzen";

        #region Request
        public bool menzen;
        #endregion

        public SetMenzenEvent(EventBase parent, int playerId, bool menzen) : base(parent, playerId) {
            this.menzen = menzen;
        }
    }
}