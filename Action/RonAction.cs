using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override string name => "ron";
        public RonAction(Player player, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }
}