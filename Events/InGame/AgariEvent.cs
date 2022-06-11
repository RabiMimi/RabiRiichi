using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Patterns;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Events.InGame {
    public class AgariInfo : IRabiPlayerMessage {
        [RabiBroadcast] public int playerId { get; init; }
        [RabiBroadcast] public readonly ScoreStorage scores;

        public AgariInfo(int playerId, ScoreStorage scores) {
            this.playerId = playerId;
            this.scores = scores;
        }
    }

    [RabiMessage]
    public class AgariInfoList : IEnumerable<AgariInfo> {
        [RabiBroadcast] private readonly List<AgariInfo> agariInfos;
        [RabiBroadcast] public readonly int fromPlayer;
        [RabiBroadcast] public readonly GameTile incoming;
        public AgariInfoList(int fromPlayer, GameTile incoming, params AgariInfo[] agariInfos) {
            this.fromPlayer = fromPlayer;
            this.incoming = incoming;
            this.agariInfos = agariInfos.ToList();
        }

        public void Add(AgariInfo info) => agariInfos.Add(info);

        public IEnumerator<AgariInfo> GetEnumerator()
            => agariInfos.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => agariInfos.GetEnumerator();

        public int Count => agariInfos.Count;
        public AgariInfo this[int index] => agariInfos[index];
    }

    [RabiMessage]
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