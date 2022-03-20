using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class NextGameEvent : EventBase {
        public override string name => "next_game";
        #region Request
        /// <summary> 是否换庄 </summary>
        [RabiBroadcast] public readonly bool switchBanker;
        /// <summary> 是否流局 </summary>
        [RabiBroadcast] public readonly bool isRyuukyoku;
        #endregion

        #region Response
        /// <summary> 轮数 </summary>
        [RabiBroadcast] public int nextRound;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int nextBanker;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int nextHonba;
        /// <summary> 立直棒 </summary>
        [RabiBroadcast] public int riichiStick;
        #endregion

        public NextGameEvent(EventBase parent, bool switchBanker, bool isRyuukyoku) : base(parent) {
            this.switchBanker = switchBanker;
            this.isRyuukyoku = isRyuukyoku;
        }
    }
}