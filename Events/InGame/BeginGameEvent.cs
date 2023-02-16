using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
    public class BeginGameEvent : EventBase {
        public override string name => "begin_game";

        #region Request
        /// <summary> 轮数 </summary>
        [RabiBroadcast] public int round;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int dealer;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        /// <summary> 立直棒数量 </summary>
        [RabiBroadcast] public int riichiStick;
        #endregion

        #region Response
        /// <summary> 牌山总牌数 </summary>
        [RabiBroadcast] public int remainingTiles;
        #endregion Response

        public BeginGameEvent(EventBase parent, int round, int dealer, int honba, int riichiStick) : base(parent) {
            this.round = round;
            this.dealer = dealer;
            this.honba = honba;
            this.riichiStick = riichiStick;
        }
    }
}