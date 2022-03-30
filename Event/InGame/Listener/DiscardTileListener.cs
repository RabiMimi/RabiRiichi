using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Core;
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
            AddPlayerAction(ev);
            ev.Q.Queue(ev.waitEvent);
            return Task.CompletedTask;
        }

        public static void AddPlayerAction(DiscardTileEvent discardEv) {
            var eventBuilder = discardEv.waitEvent.eventBuilder;
            var ev = discardEv.waitEvent;
            ev.inquiry.AddHandler<ChiiAction>((action) => {
                var option = (ChooseTilesActionOption)action.chosen;
                eventBuilder.AddEvent(new ClaimTileEvent(ev, action.playerId, new Shun(option.tiles), discardEv.tile));
            });
            ev.inquiry.AddHandler<PonAction>((action) => {
                var option = (ChooseTilesActionOption)action.chosen;
                eventBuilder.AddEvent(new ClaimTileEvent(ev, action.playerId, new Kou(option.tiles), discardEv.tile));
            });
            ev.inquiry.AddHandler<KanAction>((action) => {
                var option = (ChooseTilesActionOption)action.chosen;
                eventBuilder.AddEvent(new ClaimTileEvent(ev, action.playerId, new Kan(option.tiles), discardEv.tile));
            });
            ev.inquiry.AddHandler<RonAction>((action) => {
                eventBuilder.AddAgari(ev, discardEv.playerId, discardEv.tile, action.agariInfo);
            });
            ev.OnFinish += () => {
                if (discardEv is RiichiEvent) {
                    var riichiEv = new SetRiichiEvent(discardEv, discardEv.playerId, discardEv.tile, ev.game.IsFirstJun);
                    // 巡目增加说明立直牌被鸣牌或进入下一个玩家的回合，更新立直状态
                    // 如果立直牌被荣和，立直棒在和后才会放
                    new EventListener<IncreaseJunEvent>(ev.bus)
                        .EarlyPrepare((_) => {
                            ev.Q.Queue(riichiEv);
                            return Task.CompletedTask;
                        }, 1)
                        .ScopeTo(EventScope.Game);
                }
                if (ev.responseEvents.Count == 0) {
                    ev.Q.Queue(new NextPlayerEvent(ev, discardEv.playerId));
                }
            };
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
            eventBus.Subscribe<DiscardTileEvent>(ExecuteDiscardTile, EventPriority.Execute);
        }
    }
}