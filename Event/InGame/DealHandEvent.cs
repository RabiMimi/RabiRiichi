﻿using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class DealHandEvent : PrivatePlayerEvent {
        public override string name => "deal_hand";

        #region Response
        [RabiPrivate] public Tiles tiles;
        #endregion

        public DealHandEvent(Game game, int playerId) : base(game, playerId) { }
    }
}
