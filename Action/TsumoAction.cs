using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class TsumoAction : ConfirmAction {
        public override string id => "tsumo";
        public TsumoAction(Player player, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }
}