using RabiRiichi.Communication;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;


namespace RabiRiichi.Event.InGame {
    public class AgariInfo : IRabiPlayerMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public int playerId { get; init; }
        public readonly ScoreStorage scores;

        public AgariInfo(int playerId, ScoreStorage scores) {
            this.playerId = playerId;
            this.scores = scores;
        }
    }

    public class AgariInfoList : List<AgariInfo> {
        public readonly int fromPlayer;
        public readonly GameTile incoming;
        public AgariInfoList(int fromPlayer, GameTile incoming, params AgariInfo[] agariInfos) : base(agariInfos) {
            this.fromPlayer = fromPlayer;
            this.incoming = incoming;
        }
    }

    [RabiBroadcast]
    public class AgariEvent : EventBase {
        public override string name => "agari";
        #region Request
        [RabiBroadcast] public bool isTsumo => agariInfos.incoming.IsTsumo;
        [RabiBroadcast] public readonly AgariInfoList agariInfos;
        #endregion

        public AgariEvent(Game game, AgariInfoList info) : base(game) {
            agariInfos = info;
        }
    }
}