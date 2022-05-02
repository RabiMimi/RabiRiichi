using RabiRiichi.Core;
using RabiRiichi.Pattern;
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
                    int scoreChange = info.scores.result.BaseScore * (toPlayer.IsDealer ? 6 : 4);
                    scoreChange += ev.game.config.honbaPoints * (ev.game.config.playerCount - 1) * ev.game.info.honba;
                    ev.scoreChange.Add(new ScoreTransfer(fromPlayer.id, toPlayer.id, scoreChange, ScoreTransferReason.Ron));
                }
            }
            // 立直棒
            if (ev.agariInfos.Count > 0) {
                int agariPlayer = ev.agariInfos.MinBy(info => fromPlayer.Dist(info.playerId)).playerId;
                foreach (var player in ev.game.players) {
                    player.hand.riichiStick = 0;
                }
                if (ev.game.info.riichiStick > 0) {
                    int scoreChange = ev.game.info.riichiStick * ev.game.config.riichiPoints;
                    ev.scoreChange.Add(new ScoreTransfer(-1, agariPlayer, scoreChange, ScoreTransferReason.Accumulated));
                    ev.game.info.riichiStick = 0;
                }
            }
            ev.Q.Queue(new ApplyScoreEvent(ev, ev.scoreChange));
            return Task.CompletedTask;
        }

        public static Task CalcPao(CalcScoreEvent ev) {
            if (!ev.game.config.allowPao) {
                return Task.CompletedTask;
            }
            foreach (var info in ev.agariInfos) {
                foreach (var score in info.scores) {
                    score.Source.ResolvePao(ev.game.GetPlayer(info.playerId), ev.scoreChange);
                }
            }
            return Task.CompletedTask;
        }

        private static IEnumerable<ScoreTransfer> HandleTsumo(Player tsumoPlayer, Game game, AgariInfo info) {
            int score = info.scores.result.BaseScore;
            if (tsumoPlayer.IsDealer) {
                score *= 2;
            }
            foreach (var player in game.players.Where(player => !player.hand.agari)) {
                int scoreChange = player.IsDealer ? score * 2 : score;
                score += game.config.honbaPoints * game.info.honba;
                yield return new ScoreTransfer(player.id, tsumoPlayer.id, scoreChange, ScoreTransferReason.Tsumo);
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<CalcScoreEvent>(CalcScore, EventPriority.Execute);
            eventBus.Subscribe<CalcScoreEvent>(CalcPao, EventPriority.Execute - EventPriority.STEP);
        }
    }
}