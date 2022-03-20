using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class CalcScoreListener {
        public static Task CalcScore(CalcScoreEvent ev) {
            // TODO: 本场棒
            var fromPlayer = ev.game.GetPlayer(ev.agariInfos.fromPlayer);
            foreach (var info in ev.agariInfos) {
                var toPlayer = ev.game.GetPlayer(info.playerId);
                if (fromPlayer.SamePlayer(toPlayer)) {
                    // 自摸
                    ev.scoreChange.AddRange(HandleTsumo(fromPlayer, ev.game, info));
                } else {
                    // 荣和
                    int scoreChange = info.scores.cachedResult.BaseScore * (toPlayer.IsBanker ? 6 : 4);
                    ev.scoreChange.Add(new ScoreTransfer(fromPlayer.id, toPlayer.id, scoreChange));
                }
            }
            // 立直棒
            if (ev.agariInfos.Count > 0) {
                int agariPlayer = ev.agariInfos[0].playerId;
                foreach (var player in ev.game.players) {
                    if (player.hand.riichiStick == 0) {
                        continue;
                    }
                    int scoreChange = player.hand.riichiStick * ev.game.config.riichiPoints;
                    player.hand.riichiStick = 0;
                    ev.scoreChange.Add(new ScoreTransfer(player.id, agariPlayer, scoreChange));
                }
                if (ev.game.info.riichiStick > 0) {
                    int scoreChange = ev.game.info.riichiStick * ev.game.config.riichiPoints;
                    ev.scoreChange.Add(new ScoreTransfer(-1, agariPlayer, scoreChange));
                    ev.game.info.riichiStick = 0;
                }
            }
            ev.bus.Queue(new ApplyScoreEvent(ev, ev.scoreChange));
            return Task.CompletedTask;
        }

        private static IEnumerable<ScoreTransfer> HandleTsumo(Player tsumoPlayer, Game game, AgariInfo info) {
            int score = info.scores.cachedResult.BaseScore;
            if (tsumoPlayer.IsBanker) {
                score *= 2;
            }
            foreach (var player in game.players.Where(player => !player.hand.agari)) {
                int scoreChange = player.IsBanker ? score * 2 : score;
                yield return new ScoreTransfer(player.id, tsumoPlayer.id, scoreChange);
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<CalcScoreEvent>(CalcScore, EventPriority.Execute);
        }
    }
}