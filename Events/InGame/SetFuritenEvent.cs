using RabiRiichi.Communication;

namespace RabiRiichi.Events.InGame {
    public abstract class SetFuritenEvent : PrivatePlayerEvent {
        #region Request
        [RabiBroadcast] public bool isFuriten;
        #endregion

        public SetFuritenEvent(EventBase parent, int playerId, bool isFuriten) : base(parent, playerId) {
            this.isFuriten = isFuriten;
        }
    }

    public class SetTempFuritenEvent : SetFuritenEvent {
        public override string name => "set_temp_furiten";

        public SetTempFuritenEvent(EventBase parent, int playerId, bool isFuriten) : base(parent, playerId, isFuriten) { }
    }

    public class SetRiichiFuritenEvent : SetFuritenEvent {
        public override string name => "set_riichi_furiten";

        public SetRiichiFuritenEvent(EventBase parent, int playerId, bool isFuriten) : base(parent, playerId, isFuriten) { }
    }

    public class SetDiscardFuritenEvent : SetFuritenEvent {
        public override string name => "set_discard_furiten";

        public SetDiscardFuritenEvent(EventBase parent, int playerId, bool isFuriten) : base(parent, playerId, isFuriten) { }
    }
}