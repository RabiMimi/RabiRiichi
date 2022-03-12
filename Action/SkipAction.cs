using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class SkipAction : ConfirmAction {
        public override string name => "skip";

        public SkipAction(Player player, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.Skip + priorityDelta;
        }
    }
}