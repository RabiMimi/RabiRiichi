using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent e) {
            e.game.info.round = e.round;
            e.game.info.banker = e.banker;
            e.game.info.honba = e.honba;
            e.game.info.Reset();
            foreach (var player in e.game.players) {
                player.Reset();
            }
            return Task.CompletedTask;
        }

        public static Task AfterUpdateInfo(BeginGameEvent ev) {
            var bus = ev.bus;
            int banker = ev.game.info.banker;
            for (int i = 0; i < ev.game.config.playerCount; i++) {
                int playerId = (i + banker) % ev.game.config.playerCount;
                bus.Queue(new DealHandEvent(ev.game, playerId));
            }
            bus.Queue(new RevealDoraEvent(ev.game));
            var lastEvent = new IncreaseJunEvent(ev.game, banker);
            bus.Queue(lastEvent);
            AfterBankerDealHand(lastEvent, ev.inquiry).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        private static async Task AfterBankerDealHand(IncreaseJunEvent ev, MultiPlayerInquiry inquiry) {
            try {
                await ev.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            var freeTiles = ev.player.hand.freeTiles;
            var lastTile = freeTiles[^1];
            freeTiles.RemoveAt(freeTiles.Count - 1);
            foreach (var resolver in GetBankerFirstJunResolvers(ev.game)) {
                resolver.Resolve(ev.player, lastTile, inquiry);
            }
            inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            var waitEv = new WaitPlayerActionEvent(ev.game, inquiry);
            ev.bus.Queue(waitEv);
            await DrawTileListener.AfterPlayerAction(waitEv, DiscardReason.Draw);
        }

        private static IEnumerable<ResolverBase> GetBankerFirstJunResolvers(Game game) {
            if (game.TryGet<PlayTileResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (game.TryGet<RiichiResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (game.TryGet<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (game.TryGet<TenhouResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
            eventBus.Register<BeginGameEvent>(AfterUpdateInfo, EventPriority.After);
        }
    }
}