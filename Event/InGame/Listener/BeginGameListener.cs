using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent ev) {
            ev.game.info.round = ev.round;
            ev.game.info.banker = ev.banker;
            ev.game.info.honba = ev.honba;
            ev.game.info.Reset();
            foreach (var player in ev.game.players) {
                player.Reset();
            }
            var bus = ev.bus;
            int banker = ev.game.info.banker;
            for (int i = 0; i < ev.game.config.playerCount; i++) {
                int playerId = (i + banker) % ev.game.config.playerCount;
                bus.Queue(new DealHandEvent(ev.game, playerId));
            }
            bus.Queue(new RevealDoraEvent(ev.game));
            var lastEvent = new IncreaseJunEvent(ev.game, banker);
            bus.Queue(lastEvent);
            lastEvent.OnFinish(() => {
                AfterBankerDealHand(lastEvent, ev.waitEvent);
            });
            return Task.CompletedTask;
        }

        private static void AfterBankerDealHand(IncreaseJunEvent ev, WaitPlayerActionEvent waitEv) {
            var freeTiles = ev.player.hand.freeTiles;
            var lastTile = freeTiles[^1];
            freeTiles.RemoveAt(freeTiles.Count - 1);
            foreach (var resolver in GetBankerFirstJunResolvers(ev.game)) {
                resolver.Resolve(ev.player, lastTile, waitEv.inquiry);
            }
            waitEv.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            DrawTileListener.AddActionHandler(waitEv, DiscardReason.Draw);
            ev.bus.Queue(waitEv);
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
        }
    }
}