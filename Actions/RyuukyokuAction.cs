using RabiRiichi.Communication;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Actions;

namespace RabiRiichi.Actions {

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