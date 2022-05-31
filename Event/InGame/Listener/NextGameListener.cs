using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class NextGameListener {
        public static Task PrepareNextGame(NextGameEvent ev) {
            var info = ev.game.info;
            if (!ev.switchDealer) {
                ev.nextRound = info.round;
                ev.nextDealer = info.dealer;
                ev.nextHonba = info.honba + 1;
                return Task.CompletedTask;
            }
            if (ev.isRyuukyoku) {
                ev.nextHonba = info.honba + 1;
            } else {
                ev.nextHonba = 0;
            }
            ev.nextDealer = ev.game.NextPlayerId(info.dealer);
            ev.nextRound = ev.nextDealer == 0 ? info.round + 1 : info.round;
            return Task.CompletedTask;
        }

        public static Task ExecuteNextGame(NextGameEvent ev) {
            var info = ev.game.info;
            var players = ev.game.players;
            // 累计立直棒
            foreach (var player in players) {
                info.riichiStick += player.hand.riichiStick;
                player.points -= player.hand.riichiStick * ev.game.config.riichiPoints;
                player.hand.riichiStick = 0;
            }
            if (info.config.suddenDeath && players.Any(p => p.points < 0)) {
                // 击飞
                ev.Q.Queue(new StopGameEvent(ev));
                return Task.CompletedTask;
            }
            if (ev.nextRound >= info.config.totalRound) {
                if (players.Any(p => p.points >= info.config.finishPoints)) {
                    // 游戏结束
                    ev.Q.Queue(new StopGameEvent(ev));
                    return Task.CompletedTask;
                }
            }
            ev.Q.Queue(new BeginGameEvent(ev, ev.nextRound, ev.nextDealer, ev.nextHonba));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<NextGameEvent>(PrepareNextGame, EventPriority.Prepare);
            eventBus.Subscribe<NextGameEvent>(ExecuteNextGame, EventPriority.Execute);
        }
    }
}