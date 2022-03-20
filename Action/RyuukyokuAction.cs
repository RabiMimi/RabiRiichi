using RabiRiichi.Communication;
using RabiRiichi.Event.InGame;


namespace RabiRiichi.Action {

    public class RyuukyokuAction : ConfirmAction {
        public override string name => "ryuukyoku";
        [RabiBroadcast] public readonly RyuukyokuEvent ev;
        public RyuukyokuAction(int playerId, RyuukyokuEvent ev, int priorityDelta = 0) : base(playerId) {
            this.ev = ev;
            this.priority = ActionPriority.Ryuukyoku + priorityDelta;
        }
    }
}