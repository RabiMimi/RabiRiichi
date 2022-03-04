using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override string id => "ron";
        public RonAction(Player player, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }
}