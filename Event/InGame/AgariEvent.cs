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
        public class Builder : IEventBuilder {
            public readonly Game game;
            public readonly AgariInfoList agariInfos;
            public Builder(Game game, int fromPlayer, GameTile incoming) {
                this.game = game;
                agariInfos = new AgariInfoList(fromPlayer, incoming);
            }
            public Builder Add(AgariInfo agariInfo) {
                agariInfos.Add(agariInfo);
                return this;
            }
            public EventBase Build() {
                if (agariInfos.Count == 0) {
                    return null;
                }
                return new AgariEvent(game, agariInfos);
            }
        }
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