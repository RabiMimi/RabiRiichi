using RabiRiichi.Riichi;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class RyuukyokuListener {
        private const int NAGASHI_MANGAN_BASE_PT = 2000;
        private const int TENPAI_BASE_PT = 1000;

        private static bool IsNagashiMangan(Hand hand) {
            // 全是幺九牌 且没有被他人鸣牌
            return hand.discarded.All(tile => tile.tile.Is19Z && tile.playerId == hand.player.id);
        }

        public static Task PrepareEndGame(EndGameRyuukyokuEvent ev) {
            ev.remainingPlayers = ev.game.players
                .Where(player => !player.hand.agari)
                .Select(player => player.id)
                .ToArray();
            ev.nagashiManganPlayers = ev.remainingPlayers
                .Where(player => IsNagashiMangan(ev.game.GetPlayer(player).hand))
                .ToArray();
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
                        int score = NAGASHI_MANGAN_BASE_PT;
                        if (ev.game.info.banker == player || ev.game.info.banker == manganPlayer) {
                            score *= 2;
                        }
                        ev.AddScoreTransfer(player, manganPlayer, score);
                    }
                }
            } else {
                // 流局
                var payers = ev.remainingPlayers.Except(ev.tenpaiPlayers).ToArray();
                if (payers.Length == 2 && ev.tenpaiPlayers.Length == 2) {
                    int pt = (int)(TENPAI_BASE_PT * 1.5);
                    ev.AddScoreTransfer(payers[0], ev.tenpaiPlayers[0], pt);
                    ev.AddScoreTransfer(payers[1], ev.tenpaiPlayers[1], pt);
                } else if (payers.Length == 1) {
                    foreach (var payee in ev.tenpaiPlayers) {
                        ev.AddScoreTransfer(payers[0], payee, TENPAI_BASE_PT);
                    }
                } else if (ev.tenpaiPlayers.Length == 1) {
                    foreach (var payer in payers) {
                        ev.AddScoreTransfer(payer, ev.tenpaiPlayers[0], TENPAI_BASE_PT);
                    }
                }
            }
            ev.bus.Queue(new ApplyScoreEvent(ev.game, ev.scoreChange));
            ev.bus.Queue(new NextGameEvent(ev.game, !ev.tenpaiPlayers.Contains(ev.game.info.banker), true));
            return Task.CompletedTask;
        }

        public static Task ExecuteMidGameRyuukyoku(MidGameRyuukyokuEvent ev) {
            ev.bus.Queue(new ApplyScoreEvent(ev.game, ev.scoreChange));
            ev.bus.Queue(new NextGameEvent(ev.game, false, true));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<EndGameRyuukyokuEvent>(PrepareEndGame, EventPriority.Prepare);
            eventBus.Register<EndGameRyuukyokuEvent>(ExecuteEndGame, EventPriority.Execute);
            eventBus.Register<MidGameRyuukyokuEvent>(ExecuteMidGameRyuukyoku, EventPriority.Execute);
        }
    }
}