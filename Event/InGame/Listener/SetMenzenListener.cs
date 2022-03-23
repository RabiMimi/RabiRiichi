using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetMenzenListener {
        public static Task ExecuteMenzen(SetMenzenEvent ev) {
            ev.player.hand.menzen = ev.menzen;
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SetMenzenEvent>(ExecuteMenzen, EventPriority.Execute);
        }
    }
}