using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";
        #region request
        [RabiBroadcast] public readonly int count;
        #endregion

        #region  response
        [RabiBroadcast] public List<GameTile> tiles;
        #endregion

        public DealHandEvent(EventBase parent, int playerId, int count) : base(parent, playerId) {
            this.count = count;
        }
    }
}
