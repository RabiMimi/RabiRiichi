using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class AgariListener {
        public static Task ExecuteAgari(AgariEvent ev) {
            foreach (var info in ev.agariInfos) {
                ev.game.GetPlayer(info.playerId).hand.agariTile = ev.agariInfos.incoming;
            }
            var calcScoreEv = new CalcScoreEvent(ev.game, ev.agariInfos);
            ev.bus.Queue(calcScoreEv);
            AfterCalcScore(calcScoreEv, ev).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static async Task AfterCalcScore(CalcScoreEvent ev, AgariEvent agariEv) {
            try {
                await ev.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            ev.bus.Queue(new ApplyScoreEvent(ev.game, ev.scoreChange));
            bool bankerRon = agariEv.agariInfos.Any(info => info.playerId == ev.game.info.banker);
            ev.bus.Queue(new NextGameEvent(ev.game, !bankerRon, false));
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<AgariEvent>(ExecuteAgari, EventPriority.Execute);
        }
    }
}