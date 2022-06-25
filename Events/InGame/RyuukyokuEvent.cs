using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;
using System;
using System.Linq;

namespace RabiRiichi.Events.InGame {
    /// <summary>
    /// 所有流局事件继承该类
    /// </summary>
    public abstract class RyuukyokuEvent : EventBase {

        #region Request
        [RabiBroadcast] public readonly ScoreTransferList scoreChange;
        #endregion

        public RyuukyokuEvent(EventBase parent) : base(parent) {
            scoreChange = new ScoreTransferList();
        }

        public void AddScoreTransfer(int from, int to, long points, ScoreTransferReason reason)
            => scoreChange.Add(new ScoreTransfer(from, to, points, reason));

        public virtual RyuukyokuEventMsg ToProto() {
            var ret = new RyuukyokuEventMsg();
            ret.ScoreChange.AddRange(scoreChange.Select(x => x.ToProto()));
            return ret;
        }
    }

    public class EndGameRyuukyokuEvent : RyuukyokuEvent {
        public override string name => "end_game_ryuukyoku";

        #region Response
        [RabiBroadcast] public int[] remainingPlayers = Array.Empty<int>();
        [RabiBroadcast] public int[] nagashiManganPlayers = Array.Empty<int>();
        [RabiBroadcast] public int[] tenpaiPlayers = Array.Empty<int>();
        #endregion

        public EndGameRyuukyokuEvent(EventBase parent) : base(parent) { }

        public override RyuukyokuEventMsg ToProto() {
            var ret = base.ToProto();
            ret.EndGameRyuukyoku = new EndGameRyuukyokuEventMsg();
            ret.EndGameRyuukyoku.RemainingPlayers.AddRange(remainingPlayers);
            ret.EndGameRyuukyoku.NagashiManganPlayers.AddRange(nagashiManganPlayers);
            ret.EndGameRyuukyoku.TenpaiPlayers.AddRange(tenpaiPlayers);
            return ret;
        }
    }

    public abstract class MidGameRyuukyokuEvent : RyuukyokuEvent {
        public MidGameRyuukyokuEvent(EventBase parent) : base(parent) { }

        public override RyuukyokuEventMsg ToProto() {
            var ret = base.ToProto();
            ret.MidGameRyuukyoku = new MidGameRyuukyokuEventMsg {
                Name = name,
            };
            return ret;
        }
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