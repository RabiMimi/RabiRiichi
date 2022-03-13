using System.Threading.Tasks;


namespace RabiRiichi.Action {
    public class SkipAction : ConfirmAction {
        public override string name => "skip";

        public SkipAction(int playerId, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.Skip + priorityDelta;
        }
    }
}