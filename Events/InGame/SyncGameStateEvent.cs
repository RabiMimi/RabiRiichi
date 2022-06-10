using RabiRiichi.Communication;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
    public class SyncGameStateEvent : PrivatePlayerEvent {
        public override string name => "sync_state";

        #region Response
        [RabiPrivate] public readonly Dictionary<string, IRabiMessage> states = new();
        #endregion

        public SyncGameStateEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}