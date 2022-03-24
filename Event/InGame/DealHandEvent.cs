using RabiRiichi.Communication;
using RabiRiichi.Core;


namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";
        #region request
        [RabiBroadcast] public readonly int count;
        #endregion

        #region  response
        [RabiBroadcast] public GameTiles tiles;
        #endregion

        public DealHandEvent(EventBase parent, int playerId, int count) : base(parent, playerId) {
            this.count = count;
        }
    }
}
