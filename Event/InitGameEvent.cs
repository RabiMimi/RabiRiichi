using RabiRiichi.Event.InGame;
using RabiRiichi.Riichi;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
    public class InitGameEvent : EventBase {
        public override string name => "init";
        public InitGameEvent(Game game) : base(game) { }

        public static Task OnGameInit(InitGameEvent ev) {
            ev.bus.Queue(new BeginGameEvent(ev, 0, 0, 0));
            return Task.CompletedTask;
        }

        public static void Register(EventBus bus) {
            bus.Register<InitGameEvent>(OnGameInit, EventPriority.Execute);
        }
    }
}