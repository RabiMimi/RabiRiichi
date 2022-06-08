using RabiRiichi.Core.Config;
using RabiRiichi.Util;
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
                player.points -= player.hand.riichiStick * ev.game.config.pointThreshold.riichiPoints;
                player.hand.riichiStick = 0;
            }
            if (info.config.endGamePolicy.HasAnyFlag(EndGamePolicy.PointsOutOfRange)
                && players.Any(p => !info.config.pointThreshold.ArePointsValid(p.points))) {
                // 天边
                ev.Q.QueueIfNotExist(new StopGameEvent(ev));
                return Task.CompletedTask;
            }
            if (ev.nextRound > info.config.totalRound) {
                // 已额外进行一轮庄
                ev.Q.QueueIfNotExist(new StopGameEvent(ev));
                return Task.CompletedTask;
            } else if (ev.nextRound == info.config.totalRound) {
                // 检查玩家分数
                if (players.Any(p => p.points >= info.config.pointThreshold.finishPoints)) {
                    // 终局
                    ev.Q.QueueIfNotExist(new StopGameEvent(ev));
                    return Task.CompletedTask;
                }
            }
            // 检查庄家一位
            if (ev.game.info.IsAllLast
                && ev.game.PlayersByRank[0].SamePlayer(ev.game.Dealer)
                && ev.game.Dealer.points >= info.config.pointThreshold.finishPoints) {
                if ((
                    info.config.endGamePolicy.HasAnyFlag(EndGamePolicy.DealerTenpai)
                    && ev.dealerTenpai
                ) || (
                    info.config.endGamePolicy.HasAnyFlag(EndGamePolicy.DealerAgari)
                    && ev.game.Dealer.hand.agari
                )) {
                    ev.Q.QueueIfNotExist(new StopGameEvent(ev));
                    return Task.CompletedTask;
                }
            }
            ev.Q.Queue(new BeginGameEvent(ev, ev.nextRound, ev.nextDealer, ev.nextHonba));
            return Task.CompletedTask;
        }

        public static Task InstantTerminate(EventBase ev) {
            if (ev is StopGameEvent) {
                ev.bus.Unsubscribe<EventBase>(InstantTerminate);
                return Task.CompletedTask;
            }
            var config = ev.game.config;
            if (config.endGamePolicy.HasAnyFlag(EndGamePolicy.InstantPointsOutOfRange)
                && ev.game.players.Any(p => !config.pointThreshold.ArePointsValid(p.points))) {
                ev.Q.QueueIfNotExist(new StopGameEvent(ev));
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<NextGameEvent>(PrepareNextGame, EventPriority.Prepare);
            eventBus.Subscribe<NextGameEvent>(ExecuteNextGame, EventPriority.Execute);
            eventBus.Subscribe<EventBase>(InstantTerminate, EventPriority.After - 100);
        }
    }
}