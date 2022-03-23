namespace RabiRiichi.Event.Query {
    public class SyncGameStateEvent : PrivatePlayerEvent {
        public override string name => "sync_game_state";
        public SyncGameStateEvent(EventBase parent, int playerId) : base(parent, playerId) { }
    }
}