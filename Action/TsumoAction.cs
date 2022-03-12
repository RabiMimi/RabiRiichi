using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class TsumoAction : ConfirmAction {
        public override string name => "tsumo";
        public TsumoAction(int playerId, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.Ron + priorityDelta;
        }
    }
}