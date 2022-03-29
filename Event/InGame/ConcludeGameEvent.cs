using RabiRiichi.Communication;
using RabiRiichi.Core;

namespace RabiRiichi.Event.InGame {
    public class ConcludeGameEvent : EventBase {
        public override string name => "conclude_game";

        #region Request
        public bool switchDealer;
        public bool isRyuukyoku;
        #endregion

        #region Response
        [RabiBroadcast] public Tiles doras;
        [RabiBroadcast] public Tiles uradoras;
        #endregion

        public ConcludeGameEvent(EventBase parent, bool switchDealer, bool isRyuukyoku) : base(parent) {
            this.switchDealer = switchDealer;
            this.isRyuukyoku = isRyuukyoku;
        }
    }
}