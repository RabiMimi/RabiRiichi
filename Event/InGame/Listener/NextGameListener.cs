using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class NextGameListener {
        public static Task PrepareNextGame(NextGameEvent ev) {
            var info = ev.game.info;
            if (!ev.switchBanker) {
                ev.nextRound = info.round;
                ev.nextBanker = info.banker;
                ev.nextHonba = info.honba + 1;
                return Task.CompletedTask;
            }
            if (ev.isRyuukyoku) {
                ev.nextHonba++;
            } else {
                ev.nextHonba = 0;
            }
            ev.nextBanker = ev.game.NextPlayerId(info.banker);
            ev.nextRound = ev.nextBanker == 0 ? info.round + 1 : info.round;
            return Task.CompletedTask;
        }
        public static Task ExecuteNextGame(NextGameEvent ev) {
            var info = ev.game.info;
            var players = ev.game.players;
            if (players.Any(p => p.points < 0)) {
                // 击飞
                ev.bus.Queue(new StopGameEvent(ev.game));
                return Task.CompletedTask;
            }
            if (ev.nextRound > info.config.totalRound) {
                if (players.Any(p => p.points >= info.config.finishPoints)) {
                    // 游戏结束
                    ev.bus.Queue(new StopGameEvent(ev.game));
                    return Task.CompletedTask;
                }
            }
            info.round = ev.nextRound;
            info.banker = ev.nextBanker;
            info.honba = ev.nextHonba;
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<NextGameEvent>(PrepareNextGame, EventPriority.Prepare);
            eventBus.Register<NextGameEvent>(ExecuteNextGame, EventPriority.Execute);
        }
    }
}