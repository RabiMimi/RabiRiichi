﻿using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class DealHandEvent : EventBase {
        #region Request
        public int player;
        #endregion

        #region Response
        public Tiles tiles;
        #endregion
    }
}
