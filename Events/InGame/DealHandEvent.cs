﻿using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Events.InGame;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Events.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";
        #region request
        public readonly int count;
        #endregion

        #region  response
        [RabiBroadcast] public readonly List<GameTile> tiles = new();
        #endregion

        public DealHandEvent(EventBase parent, int playerId, int count) : base(parent, playerId) {
            this.count = count;
        }
    }
}
