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

    [RabiBroadcast]
    public class AgariEvent : EventBase {
        public override string name => "agari";
        #region Request
        [RabiBroadcast] public bool isTsumo => incoming.IsTsumo;
        [RabiBroadcast] public readonly GameTile incoming;
        [RabiBroadcast] public readonly List<AgariInfo> agariInfos = new();
        #endregion

        public AgariEvent(Game game, GameTile incoming) : base(game) {
            this.incoming = incoming;
        }

        public AgariEvent AddAgari(int playerId, Scorings scorings) {
            agariInfos.Add(new AgariInfo(playerId, scorings));
            return this;
        }
    }
}