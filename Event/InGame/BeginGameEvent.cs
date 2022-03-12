using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class BeginGameEvent : EventBase {
        public override string name => "begin_game";

        #region Request
        /// <summary> 场风 </summary>
        [RabiBroadcast] public Wind wind;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int round;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        #endregion

        #region Response
        #endregion

        public BeginGameEvent(Game game, Wind wind, int round, int honba) : base(game) {
            this.wind = wind;
            this.round = round;
            this.honba = honba;
        }
    }
}