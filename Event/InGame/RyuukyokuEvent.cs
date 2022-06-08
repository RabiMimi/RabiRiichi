using RabiRiichi.Communication;
using System;


namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 所有流局事件继承该类
    /// </summary>
    public abstract class RyuukyokuEvent : EventBase {

        #region Request
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public RyuukyokuEvent(EventBase parent) : base(parent) {
            scoreChange = new ScoreTransferList(game.config.playerCount);
        }

        public void AddScoreTransfer(int from, int to, long points, ScoreTransferReason reason)
            => scoreChange.Add(new ScoreTransfer(from, to, points, reason));
    }

    public class EndGameRyuukyokuEvent : RyuukyokuEvent {
        public override string name => "end_game_ryuukyoku";

        #region Response
        [RabiBroadcast] public int[] remainingPlayers = Array.Empty<int>();
        [RabiBroadcast] public int[] nagashiManganPlayers = Array.Empty<int>();
        [RabiBroadcast] public int[] tenpaiPlayers = Array.Empty<int>();
        #endregion

        public EndGameRyuukyokuEvent(EventBase parent) : base(parent) { }
    }

    public abstract class MidGameRyuukyokuEvent : RyuukyokuEvent {
        public MidGameRyuukyokuEvent(EventBase parent) : base(parent) { }
    }

    public class SuufonRenda : MidGameRyuukyokuEvent {
        public override string name => "suufon_renda";

        public SuufonRenda(EventBase parent) : base(parent) { }
    }

    public class KyuushuKyuuhai : MidGameRyuukyokuEvent {
        public override string name => "kyuushu_kyuuhai";

        public KyuushuKyuuhai(EventBase parent) : base(parent) { }
    }

    public class SuuchaRiichi : MidGameRyuukyokuEvent {
        public override string name => "suucha_riichi";

        public SuuchaRiichi(EventBase parent) : base(parent) { }
    }

    public class Sanchahou : MidGameRyuukyokuEvent {
        public override string name => "triple_ron";

        public Sanchahou(EventBase parent) : base(parent) { }
    }

    public class SuukanSanra : MidGameRyuukyokuEvent {
        public override string name => "suukan_sanra";

        public SuukanSanra(EventBase parent) : base(parent) { }
    }
}