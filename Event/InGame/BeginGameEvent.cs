using RabiRiichi.Communication;

namespace RabiRiichi.Event.InGame {
    public class BeginGameEvent : EventBase {
        public override string name => "begin_game";

        #region Request
        /// <summary> 轮数 </summary>
        [RabiBroadcast] public int round;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int banker;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        #endregion

        #region Response
        public readonly WaitPlayerActionEvent waitEvent;
        #endregion

        public BeginGameEvent(EventBase parent, int round, int banker, int honba) : base(parent) {
            this.round = round;
            this.banker = banker;
            this.honba = honba;
            this.waitEvent = new WaitPlayerActionEvent(this);
        }
    }
}