using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class RonAction : ConfirmAction {
        public override int Priority => 6000;
        public RonAction(Player player) : base(player) {}
    }
}