using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    class DealHandEvent : EventBase {
        #region Request
        public int player;
        #endregion

        #region Reponse
        public Hais hand;
        #endregion
    }
}
