using RabiRiichi.Communication;

namespace RabiRiichi.Event.InGame {
    public class BeginGameEvent : EventBase {
        public override string name => "begin_game";

        #region Request
        /// <summary> 轮数 </summary>
        [RabiBroadcast] public int round;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int dealer;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        #endregion

        public BeginGameEvent(EventBase parent, int round, int dealer, int honba) : base(parent) {
            this.round = round;
            this.dealer = dealer;
            this.honba = honba;
        }
    }
}