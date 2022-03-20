using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

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
        public readonly MultiPlayerInquiry inquiry;
        #endregion

        public BeginGameEvent(Game game, int round, int banker, int honba) : base(game) {
            this.round = round;
            this.banker = banker;
            this.honba = honba;
            this.inquiry = new MultiPlayerInquiry(game.info);
        }
    }
}