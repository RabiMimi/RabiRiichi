using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public class NextGameEvent : EventBase {
        public override string name => "next_game";
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

        public NextGameEvent(ConcludeGameEvent parent) : base(parent) { }

        public NextGameEventMsg ToProto() {
            return new NextGameEventMsg {
                NextRound = nextRound,
                NextDealer = nextDealer,
                NextHonba = nextHonba,
                RiichiStick = riichiStick,
            };
        }
    }
}