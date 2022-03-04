using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override string id => "ron";
        public override int priority => ActionPriority.Ron;
        public RonAction(Player player) : base(player) { }
    }
}