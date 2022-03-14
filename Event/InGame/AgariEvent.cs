using RabiRiichi.Interact;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;


namespace RabiRiichi.Event.InGame {
    public class AgariInfo : IRabiPlayerMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public int playerId { get; init; }
        public readonly Scorings scorings;

        public AgariInfo(int playerId, Scorings scorings) {
            this.playerId = playerId;
            this.scorings = scorings;
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

        public AgariEvent AddAgari(int playerId, Scorings scorings) {
            agariInfos.Add(new AgariInfo(playerId, scorings));
            return this;
        }
    }
}