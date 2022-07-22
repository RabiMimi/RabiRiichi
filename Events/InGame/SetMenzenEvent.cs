using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
    public class SetMenzenEvent : PlayerEvent {
        public override string name => "set_menzen";

        #region Request
        [RabiBroadcast] public bool menzen;
        #endregion

        public SetMenzenEvent(EventBase parent, int playerId, bool menzen) : base(parent, playerId) {
            this.menzen = menzen;
        }
    }
}