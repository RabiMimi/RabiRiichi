using RabiRiichi.Communication;
using RabiRiichi.Event.InGame;


namespace RabiRiichi.Action {

    public class RyuukyokuAction : ConfirmAction {
        public override string name => "ryuukyoku";
        public readonly RyuukyokuEvent ev;
        [RabiBroadcast] public string reason => ev.name;
        public RyuukyokuAction(int playerId, RyuukyokuEvent ev, int priorityDelta = 0) : base(playerId) {
            this.ev = ev;
            this.priority = ActionPriority.Ryuukyoku + priorityDelta;
        }
    }
}