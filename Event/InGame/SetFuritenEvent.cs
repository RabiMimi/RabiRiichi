using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public abstract class SetFuritenEvent : PrivatePlayerEvent {
        #region Request
        [RabiBroadcast] public bool isFuriten;
        #endregion

        public SetFuritenEvent(Game game, int playerId, bool isFuriten) : base(game, playerId) {
            this.isFuriten = isFuriten;
        }
    }

    public class SetTempFuritenEvent : SetFuritenEvent {
        public override string name => "set_temp_furiten";

        public SetTempFuritenEvent(Game game, int playerId, bool isFuriten) : base(game, playerId, isFuriten) { }
    }

    public class SetRiichiFuritenEvent : SetFuritenEvent {
        public override string name => "set_riichi_furiten";

        public SetRiichiFuritenEvent(Game game, int playerId, bool isFuriten) : base(game, playerId, isFuriten) { }
    }

    public class SetDiscardFuritenEvent : SetFuritenEvent {
        public override string name => "set_discard_furiten";

        public SetDiscardFuritenEvent(Game game, int playerId, bool isFuriten) : base(game, playerId, isFuriten) { }
    }
}