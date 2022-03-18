using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 所有流局事件继承该类
    /// </summary>
    public abstract class RyuukyokuEvent : EventBase {

        #region Request
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public RyuukyokuEvent(Game game) : base(game) {
            scoreChange = new ScoreTransferList(game.config.playerCount);
        }

        public void AddScoreTransfer(int from, int to, int points)
            => scoreChange.Add(new ScoreTransfer(from, to, points));
    }

    public class EndGameRyuukyokuEvent : RyuukyokuEvent {
        public override string name => "end_game_ryuukyoku";

        #region Response
        [RabiBroadcast] public int nagashiManganPlayer = -1;
        #endregion

        public EndGameRyuukyokuEvent(Game game) : base(game) { }
    }

    public abstract class MidGameRyuukyokuEvent : RyuukyokuEvent {
        public MidGameRyuukyokuEvent(Game game) : base(game) { }
    }

    public class SuufonRenda : MidGameRyuukyokuEvent {
        public override string name => "suufon_renda";

        public SuufonRenda(Game game) : base(game) { }
    }

    public class KyuushuKyuuhai : MidGameRyuukyokuEvent {
        public override string name => "kyuushu_kyuuhai";

        public KyuushuKyuuhai(Game game) : base(game) { }
    }

    public class SuuchaRiichi : MidGameRyuukyokuEvent {
        public override string name => "suucha_riichi";

        public SuuchaRiichi(Game game) : base(game) { }
    }

    public class TripleRon : MidGameRyuukyokuEvent {
        public override string name => "triple_ron";

        public TripleRon(Game game) : base(game) { }
    }

    public class SuukanSanra : MidGameRyuukyokuEvent {
        public override string name => "suukan_sanra";

        public SuukanSanra(Game game) : base(game) { }
    }
}