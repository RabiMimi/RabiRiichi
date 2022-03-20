using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DiscardTileListener {
        public static Task ExecuteDiscardTile(DiscardTileEvent ev) {
            ev.player.hand.Play(ev.tile, ev.reason);
            if (ev is RiichiEvent) {
                ev.tile.riichi = true;
            }
            var resolvers = GetDiscardTileResolvers(ev.game);
            foreach (var resolver in resolvers) {
                resolver.Resolve(ev.player, ev.tile, ev.waitEvent.inquiry);
            }
            ev.bus.Queue(ev.waitEvent);
            AfterPlayerAction(ev.waitEvent, ev).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        public static async Task AfterPlayerAction(WaitPlayerActionEvent ev, DiscardTileEvent discardEv) {
            try {
                await ev.WaitForFinish;
            } catch (OperationCanceledException) {
                return;
            }
            var eventBuilder = new MultiEventBuilder();
            var resp = ev.inquiry.responses;
            foreach (var action in resp) {
                if (action is ChiiAction chii) {
                    var option = (ChooseTilesActionOption)chii.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, chii.playerId, new Shun(option.gameTiles), discardEv.tile));
                } else if (action is PonAction pon) {
                    var option = (ChooseTilesActionOption)pon.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, pon.playerId, new Kou(option.gameTiles), discardEv.tile));
                } else if (action is KanAction kan) {
                    var option = (ChooseTilesActionOption)kan.chosen;
                    eventBuilder.AddEvent(new ClaimTileEvent(ev.game, kan.playerId, new Kan(option.gameTiles), discardEv.tile));
                } else if (action is RonAction ron) {
                    eventBuilder.AddAgari(ev.game, discardEv.playerId, discardEv.tile, ron.agariInfo);
                }
            }
            var events = eventBuilder.BuildAndQueue(ev.bus);
            if (discardEv is RiichiEvent) {
                var riichiEv = new SetRiichiEvent(ev.game, discardEv.playerId, discardEv.tile, ev.game.IsFirstJun);
                // 巡目增加说明立直牌被鸣牌或进入下一个玩家的回合，更新立直状态
                // 如果立直牌被荣和，立直棒在和后才会放
                new EventListener<IncreaseJunEvent>(ev.bus)
                    .EarlyPrepare((_) => {
                        ev.bus.Queue(riichiEv);
                        return Task.CompletedTask;
                    }, 1)
                    .ScopeTo(EventScope.Game);
            }
            if (events.Count == 0) {
                ev.bus.Queue(new NextPlayerEvent(ev.game, ev.playerId));
            }
        }

        private static IEnumerable<ResolverBase> GetDiscardTileResolvers(Game game) {
            if (game.TryGet<ChiiResolver>(out var resolver1)) {
                yield return resolver1;
            }
            if (game.TryGet<PonResolver>(out var resolver2)) {
                yield return resolver2;
            }
            if (game.TryGet<KanResolver>(out var resolver3)) {
                yield return resolver3;
            }
            if (game.TryGet<RonResolver>(out var resolver4)) {
                yield return resolver4;
            }
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DiscardTileEvent>(ExecuteDiscardTile, EventPriority.Execute);
        }
    }
}