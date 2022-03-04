using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class BeginGameEvent : EventBase {
        #region Request
        /// <summary> 场风 </summary>
        public Wind wind;
        /// <summary> 局数 </summary>
        public int round;
        /// <summary> 本场 </summary>
        public int honba;
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