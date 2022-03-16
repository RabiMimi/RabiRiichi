using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class CalcScoreListener {
        public static Task CalcScore(CalcScoreEvent ev) {
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