using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class RyuukyokuListener {
        private const int NAGASHI_MANGAN_BASE_PT = 2000;

        private static bool IsNagashiMangan(Hand hand) {
            // 全是幺九牌 且没有被他人鸣牌
            return hand.discarded.All(tile => tile.tile.Is19Z && tile.playerId == hand.player.id);
        }

        public static Task PrepareEndGame(EndGameRyuukyokuEvent ev) {
            ev.remainingPlayers = ev.game.players
                .Where(player => !player.hand.agari)
                .Select(player => player.id)
                .ToArray();
            if (ev.game.config.agariOption.HasAnyFlag(AgariOption.NagashiMangan)) {
                ev.nagashiManganPlayers = ev.remainingPlayers
                    .Where(player => IsNagashiMangan(ev.game.GetPlayer(player).hand))
                    .ToArray();
            }
            ev.tenpaiPlayers = ev.remainingPlayers
                .Where(player => ev.game.GetPlayer(player).hand.Tenpai.Count > 0)
                .ToArray();
            return Task.CompletedTask;
        }

        public static Task ExecuteEndGame(EndGameRyuukyokuEvent ev) {
            if (ev.nagashiManganPlayers.Length > 0) {
                // 流局满贯
                foreach (var player in ev.remainingPlayers.Except(ev.nagashiManganPlayers)) {
                    foreach (var manganPlayer in ev.nagashiManganPlayers) {
                        long score = NAGASHI_MANGAN_BASE_PT;
                        if (ev.game.info.dealer == player || ev.game.info.dealer == manganPlayer) {
                            score *= 2;
                        }
                        ev.AddScoreTransfer(player, manganPlayer, score, ScoreTransferReason.NagashiMangan);
                    }
                }
            } else {
                // 流局
                var payers = ev.remainingPlayers.Except(ev.tenpaiPlayers).ToArray();
                if (payers.Length == 2 && ev.tenpaiPlayers.Length == 2) {
                    long pt = ev.game.config.pointThreshold.ryuukyokuPoints[1];
                    ev.AddScoreTransfer(payers[0], ev.tenpaiPlayers[0], pt, ScoreTransferReason.Ryuukyoku);
                    ev.AddScoreTransfer(payers[1], ev.tenpaiPlayers[1], pt, ScoreTransferReason.Ryuukyoku);
                } else {
                    long pt = ev.game.config.pointThreshold.ryuukyokuPoints[0];
                    if (payers.Length == 1) {
                        foreach (var payee in ev.tenpaiPlayers) {
                            ev.AddScoreTransfer(payers[0], payee, pt, ScoreTransferReason.Ryuukyoku);
                        }
                    } else if (ev.tenpaiPlayers.Length == 1) {
                        foreach (var payer in payers) {
                            ev.AddScoreTransfer(payer, ev.tenpaiPlayers[0], pt, ScoreTransferReason.Ryuukyoku);
                        }
                    }
                }
            }
            ev.Q.Queue(new ApplyScoreEvent(ev, ev.scoreChange));
            ev.Q.Queue(new ConcludeGameEvent(ev, ConcludeGameReason.EndGameRyuukyoku, ev.tenpaiPlayers));
            return Task.CompletedTask;
        }

        public static Task ExecuteMidGameRyuukyoku(MidGameRyuukyokuEvent ev) {
            ev.Q.Queue(new ApplyScoreEvent(ev, ev.scoreChange));
            ev.Q.Queue(new ConcludeGameEvent(ev, ConcludeGameReason.MidGameRyuukyoku));
            return Task.CompletedTask;
        }

        public static Task SuufonRendaListener(NextPlayerEvent ev) {
            if (!ev.game.config.ryuukyokuTrigger.HasAnyFlag(RyuukyokuTrigger.SuufonRenda) || !ev.game.IsFirstJun) {
                return Task.CompletedTask;
            }
            Tile wind = Tile.Empty;
            foreach (var player in ev.game.players) {
                if (player.hand.discarded.Count != 1) {
                    return Task.CompletedTask;
                }
                var tile = player.hand.discarded.First().tile;
                if (!tile.IsWind) {
                    return Task.CompletedTask;
                }
                if (wind.IsEmpty) {
                    wind = tile;
                } else if (!wind.IsSame(tile)) {
                    return Task.CompletedTask;
                }
            }
            ev.Cancel();
            ev.Q.ClearEvents();
            ev.Q.Queue(new SuufonRenda(ev));
            return Task.CompletedTask;
        }

        public static Task SuuchaRiichiListener(SetRiichiEvent ev) {
            if (!ev.game.config.ryuukyokuTrigger.HasAnyFlag(RyuukyokuTrigger.SuuchaRiichi) || ev.game.players.Any(p => !p.hand.riichi || p.hand.agari)) {
                return Task.CompletedTask;
            }
            ev.Q.ClearEvents();
            ev.Q.Queue(new SuuchaRiichi(ev));
            return Task.CompletedTask;
        }

        public static Task SanchahouListener(AgariEvent ev) {
            if (!ev.game.config.ryuukyokuTrigger.HasAnyFlag(RyuukyokuTrigger.Sanchahou) || ev.agariInfos.GroupBy(info => info.playerId).Count() != 3) {
                return Task.CompletedTask;
            }
            ev.Q.ClearEvents();
            ev.Q.Queue(new Sanchahou(ev));
            return Task.CompletedTask;
        }

        public static Task SuukanSanraListener(IncreaseJunEvent ev) {
            var wall = ev.game.wall;
            if (!ev.game.config.ryuukyokuTrigger.HasAnyFlag(RyuukyokuTrigger.SuukanSanra) || wall.rinshan.Count != 0) {
                return Task.CompletedTask;
            }
            foreach (var player in ev.game.players) {
                // 四杠子，不判定流局
                if (player.hand.called.Count(gr => gr is Kan) == 4) {
                    return Task.CompletedTask;
                }
            }
            ev.Cancel();
            ev.Q.ClearEvents();
            ev.Q.Queue(new SuukanSanra(ev));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<EndGameRyuukyokuEvent>(PrepareEndGame, EventPriority.Prepare);
            eventBus.Subscribe<EndGameRyuukyokuEvent>(ExecuteEndGame, EventPriority.Execute);
            eventBus.Subscribe<MidGameRyuukyokuEvent>(ExecuteMidGameRyuukyoku, EventPriority.Execute);
            eventBus.Subscribe<NextPlayerEvent>(SuufonRendaListener, EventPriority.Prepare);
            eventBus.Subscribe<SetRiichiEvent>(SuuchaRiichiListener, EventPriority.After);
            eventBus.Subscribe<AgariEvent>(SanchahouListener, EventPriority.After);
            eventBus.Subscribe<IncreaseJunEvent>(SuukanSanraListener, EventPriority.Prepare);
        }
    }
}