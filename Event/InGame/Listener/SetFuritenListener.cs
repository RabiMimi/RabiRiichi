using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetFuritenListener {
        public static Task SetFuriten(SetFuritenEvent ev) {
            if (ev is SetRiichiFuritenEvent) {
                ev.player.hand.isRiichiFuriten = ev.isFuriten;
            } else if (ev is SetTempFuritenEvent) {
                ev.player.hand.isTempFuriten = ev.isFuriten;
            } else if (ev is SetDiscardFuritenEvent) {
                ev.player.hand.isDiscardFuriten = ev.isFuriten;
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<SetFuritenEvent>(SetFuriten, EventPriority.Execute);
        }
    }
}