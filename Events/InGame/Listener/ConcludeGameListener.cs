using RabiRiichi.Core;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class ConcludeGameListener {
        public static Task PrepareConcludeGame(ConcludeGameEvent ev) {
            ev.doras.AddRange(ev.game.wall.doras.Select(t => t.tile));
            ev.uradoras.AddRange(ev.game.wall.uradoras.Select(t => t.tile));
            return Task.CompletedTask;
        }

        public static Task ExecuteConcludeGame(ConcludeGameEvent ev) {
            ev.Q.Queue(new NextGameEvent(ev));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<ConcludeGameEvent>(PrepareConcludeGame, EventPriority.Prepare);
            eventBus.Subscribe<ConcludeGameEvent>(ExecuteConcludeGame, EventPriority.Execute);
        }
    }
}