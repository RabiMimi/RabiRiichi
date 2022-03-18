
using RabiRiichi.Communication;
using RabiRiichi.Event;
using RabiRiichi.Riichi;


namespace RabiIppatsu.Event.InGame {
    public class SetIppatsuEvent : BroadcastPlayerEvent {
        public override string name => "set_ippatsu";

        #region Request
        [RabiBroadcast] public bool ippatsu;
        #endregion

        public SetIppatsuEvent(Game game, int playerId, bool ippatsu) : base(game, playerId) {
            this.ippatsu = ippatsu;
        }
    }
}