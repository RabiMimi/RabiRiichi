namespace RabiRiichi.Action {
    public abstract class AgariAction : ConfirmAction {
        public AgariAction(int playerId, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }

    public class RonAction : AgariAction {
        public override string name => "ron";
        public RonAction(int playerId, int priorityDelta = 0) : base(playerId, priorityDelta) { }
    }

    public class TsumoAction : AgariAction {
        public override string name => "tsumo";
        public TsumoAction(int playerId, int priorityDelta = 0) : base(playerId, priorityDelta) { }
    }
}