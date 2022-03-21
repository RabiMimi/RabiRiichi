
using RabiRiichi.Communication;


namespace RabiRiichi.Event.InGame {
    public class SetIppatsuEvent : BroadcastPlayerEvent {
        public override string name => "set_ippatsu";

        #region Request
        [RabiBroadcast] public bool ippatsu;
        #endregion

        public SetIppatsuEvent(EventBase parent, int playerId, bool ippatsu) : base(parent, playerId) {
            this.ippatsu = ippatsu;
        }
    }
}