using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
    public class DealHandEvent : PlayerEvent {
        public override string name => "deal_hand";
        #region request
        [RabiBroadcast] public readonly int count;
        #endregion

        #region  response
        [RabiPrivate] public readonly List<GameTile> tiles = new();
        #endregion

        public DealHandEvent(EventBase parent, int playerId, int count) : base(parent, playerId) {
            this.count = count;
        }
    }
}
