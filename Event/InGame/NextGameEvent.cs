using RabiRiichi.Communication;

namespace RabiRiichi.Event.InGame {
    public class NextGameEvent : EventBase {
        public override string name => "next_game";
        #region Request
        /// <summary> 是否换庄 </summary>
        [RabiBroadcast] public readonly bool switchDealer;
        /// <summary> 是否流局 </summary>
        [RabiBroadcast] public readonly bool isRyuukyoku;
        /// <summary> 庄家是否听牌 </summary>
        [RabiBroadcast] public readonly bool dealerTenpai;
        #endregion

        #region Response
        /// <summary> 轮数 </summary>
        [RabiBroadcast] public int nextRound;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int nextDealer;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int nextHonba;
        /// <summary> 立直棒 </summary>
        [RabiBroadcast] public int riichiStick;
        #endregion

        public NextGameEvent(EventBase parent, bool switchDealer, bool isRyuukyoku,
            bool dealerTenpai) : base(parent) {
            this.switchDealer = switchDealer;
            this.isRyuukyoku = isRyuukyoku;
            this.dealerTenpai = dealerTenpai;
        }
    }
}