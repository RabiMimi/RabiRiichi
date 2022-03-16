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
        public AgariInfoList(int fromPlayer) {
            this.fromPlayer = fromPlayer;
        }
    }

    [RabiBroadcast]
    public class AgariEvent : EventBase {
        public override string name => "agari";
        #region Request
        [RabiBroadcast] public bool isTsumo => incoming.IsTsumo;
        [RabiBroadcast] public readonly GameTile incoming;
        [RabiBroadcast] public readonly AgariInfoList agariInfos;
        #endregion

        public AgariEvent(Game game, GameTile incoming) : base(game) {
            this.incoming = incoming;
            this.agariInfos = new AgariInfoList(incoming.fromPlayerId ?? -1);
        }

        public AgariEvent AddAgari(int playerId, ScoreStorage scores) {
            agariInfos.Add(new AgariInfo(playerId, scores));
            return this;
        }
    }
}