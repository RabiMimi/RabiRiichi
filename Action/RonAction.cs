using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override string name => "ron";
        public RonAction(int playerId, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }
}