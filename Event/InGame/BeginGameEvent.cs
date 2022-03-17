using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class BeginGameEvent : EventBase {
        public override string name => "begin_game";

        #region Request
        /// <summary> 场风 </summary>
        [RabiBroadcast] public Wind wind;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int banker;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        #endregion

        #region Response
        #endregion

        public BeginGameEvent(Game game, Wind wind, int banker, int honba) : base(game) {
            this.wind = wind;
            this.banker = banker;
            this.honba = honba;
        }
    }
}