using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class NextGameEvent : EventBase {
        public override string name => "next_game";
        #region Request
        /// <summary> 场风 </summary>
        [RabiBroadcast] public Wind wind;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int banker;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int honba;
        #endregion

        #region Response
        /// <summary> 场风 </summary>
        [RabiBroadcast] public Wind nextWind;
        /// <summary> 局数 </summary>
        [RabiBroadcast] public int nextBanker;
        /// <summary> 本场 </summary>
        [RabiBroadcast] public int nextHonba;
        #endregion
        public NextGameEvent(Game game) : base(game) { }
    }
}