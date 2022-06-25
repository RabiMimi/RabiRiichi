using Google.Protobuf.WellKnownTypes;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Sync;
using RabiRiichi.Generated.Events.InGame;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
    public class SyncGameStateEvent : PrivatePlayerEvent {
        public override string name => "sync_state";

        #region Response
        [RabiPrivate] public GameState gameState;
        [RabiPrivate] public readonly Dictionary<string, Any> extra = new();
        #endregion

        public SyncGameStateEvent(EventBase parent, int playerId) : base(parent, playerId) { }

        public SyncGameStateEventMsg ToProto(int playerId) {
            var ret = new SyncGameStateEventMsg {
                PlayerId = this.playerId,
                GameState = gameState?.ToProto(playerId),
            };
            if (this.playerId == playerId) {
                foreach (var (key, value) in extra) {
                    ret.Extra.Add(key, value);
                }
            }
            return ret;
        }
    }
}