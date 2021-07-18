using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public class DftTriggerUpdate : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override Task<bool> Handle(EventBase ev) {
            ev.game.OnUpdate();
            return Task.FromResult(false);
        }
    }
}
