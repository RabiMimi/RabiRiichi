using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Util;
using System;

namespace RabiRiichi.Events.InGame {
    [Flags]
    public enum ConcludeGameReason {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 中途流局 </summary>
        MidGameRyuukyoku = 1 << 0,
        /// <summary> 终局流局 </summary>
        EndGameRyuukyoku = 1 << 1,
        /// <summary> 和了 </summary>
        Agari = 1 << 2,
        /// <summary> 流局 </summary>
        Ryuukyoku = MidGameRyuukyoku | EndGameRyuukyoku,
    }

    public class ConcludeGameEvent : EventBase {
        public override string name => "conclude_game";

        #region Request
        /// <summary> 流局听牌玩家。没有终局流局则为null </summary>
        public int[] tenpaiPlayers;
        public ConcludeGameReason reason;
        public bool IsRyuukyoku => reason.HasAnyFlag(ConcludeGameReason.Ryuukyoku);
        #endregion

        #region Response
        [RabiBroadcast] public Tiles doras;
        [RabiBroadcast] public Tiles uradoras;
        #endregion

        public ConcludeGameEvent(EventBase parent, ConcludeGameReason reason, int[] tenpaiPlayers = null) : base(parent) {
            this.reason = reason;
            this.tenpaiPlayers = tenpaiPlayers;
        }
    }
}