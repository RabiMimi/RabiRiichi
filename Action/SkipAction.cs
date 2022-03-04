using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class SkipAction : ConfirmAction {
        public override string id => "skip";
        public override int priority => ActionPriority.Skip;

        public SkipAction(Player player) : base(player) { }
    }
}