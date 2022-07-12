using Google.Protobuf.WellKnownTypes;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Sync;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
    public class SyncGameStateEvent : PrivatePlayerEvent {
        public override string name => "sync_state";

        #region Response
        [RabiPrivate] public GameState gameState;
        [RabiPrivate] public readonly Dictionary<string, Any> extra = new();
        #endregion

        public SyncGameStateEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}