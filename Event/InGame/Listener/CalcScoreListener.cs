using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;
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
                    long scoreChange = info.scores.result.BaseScore * (toPlayer.IsDealer ? 6 : 4);
                    long honbaChange = ev.game.config.pointThreshold.honbaPoints * ev.game.info.honba;
                    ev.scoreChange.Add(new ScoreTransfer(fromPlayer.id, toPlayer.id, scoreChange, ScoreTransferReason.Ron));
                    ev.scoreChange.Add(new ScoreTransfer(fromPlayer.id, toPlayer.id, honbaChange, ScoreTransferReason.Honba));
                }
            }
            // 立直棒
            if (ev.agariInfos.Count > 0) {
                int agariPlayer = ev.agariInfos.MinBy(info => fromPlayer.Dist(info.playerId)).playerId;
                foreach (var player in ev.game.players) {
                    player.hand.riichiStick = 0;
                }
                if (ev.game.info.riichiStick > 0) {
                    long scoreChange = ev.game.info.riichiStick * ev.game.config.pointThreshold.riichiPoints;
                    ev.scoreChange.Add(new ScoreTransfer(-1, agariPlayer, scoreChange, ScoreTransferReason.Riichi));
                    ev.game.info.riichiStick = 0;
                }
            }
            return Task.CompletedTask;
        }

        public static Task CalcPao(CalcScoreEvent ev) {
            if (!ev.game.config.agariOption.HasAnyFlag(AgariOption.Pao)) {
                return Task.CompletedTask;
            }
            foreach (var info in ev.agariInfos) {
                foreach (var score in info.scores) {
                    score.Source.OnScoreTransfer(ev.game.GetPlayer(info.playerId), ev.scoreChange);
                }
            }
            return Task.CompletedTask;
        }

        private static IEnumerable<ScoreTransfer> HandleTsumo(Player tsumoPlayer, Game game, AgariInfo info) {
            long score = info.scores.result.BaseScore;
            if (tsumoPlayer.IsDealer) {
                score *= 2;
            }
            long honbaChange = game.config.HonbaPointsForOnePlayer(game.info.honba);
            foreach (var player in game.players.Where(player => !player.hand.agari)) {
                long scoreChange = player.IsDealer ? score * 2 : score;
                yield return new ScoreTransfer(player.id, tsumoPlayer.id, scoreChange, ScoreTransferReason.Tsumo);
                yield return new ScoreTransfer(player.id, tsumoPlayer.id, honbaChange, ScoreTransferReason.Honba);
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<CalcScoreEvent>(CalcScore, EventPriority.Execute);
            eventBus.Subscribe<CalcScoreEvent>(CalcPao, EventPriority.Execute - EventPriority.STEP);
        }
    }
}