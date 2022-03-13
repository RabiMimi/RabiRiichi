using System.Threading.Tasks;


namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override string name => "ron";
        public RonAction(int playerId, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.Ron + priorityDelta;
        }

        public override Task OnResponse(bool response) {
            throw new System.NotImplementedException();
        }
    }
}