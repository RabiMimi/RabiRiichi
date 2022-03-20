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
            public readonly EventBase parent;
            public readonly AgariInfoList agariInfos;
            public Builder(EventBase parent, int fromPlayer, GameTile incoming) {
                this.parent = parent;
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
                return new AgariEvent(parent, agariInfos);
            }
        }
        public override string name => "agari";
        #region Request
        [RabiBroadcast] public bool isTsumo => agariInfos.incoming.IsTsumo;
        [RabiBroadcast] public readonly AgariInfoList agariInfos;
        #endregion

        public AgariEvent(EventBase parent, AgariInfoList info) : base(parent) {
            agariInfos = info;
        }
    }
}