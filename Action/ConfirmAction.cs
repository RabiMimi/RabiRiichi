using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public abstract class ConfirmAction : PlayerAction<bool> {
        public ConfirmAction(Player player) : base(player) { }
    }
}