using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class SetMenzenEvent : BroadcastPlayerEvent {
        public override string name => "set_menzen";

        #region Request
        [RabiBroadcast] public bool menzen;
        #endregion

        public SetMenzenEvent(EventBase parent, int playerId, bool menzen) : base(parent, playerId) {
            this.menzen = menzen;
        }

        public SetMenzenEventMsg ToProto() {
            return new SetMenzenEventMsg {
                PlayerId = playerId,
                Menzen = menzen,
            };
        }
    }
}