using RabiRiichi.Action;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class AddKanListener {
        private static Task ExecuteAddKan(AddKanEvent ev) {
            if (ev.kanSource == TileSource.AnKan || ev.kanSource == TileSource.DaiMinKan) {
                ev.player.hand.AddKan(ev.kan);
            } else if (ev.kanSource == TileSource.KaKan) {
                ev.player.hand.KaKan(ev.kan);
            } else {
                return Task.CompletedTask;
            }

            if (ev.kanSource == TileSource.AnKan) {
                ev.bus.Queue(new RevealDoraEvent(ev.game, ev.playerId));
            } else {
                var revealDoraEv = new RevealDoraEvent(ev.game, ev.playerId);
                bool isRevealed = false;
                Task DelayRevealDora(EventBase _) {
                    if (!isRevealed) {
                        ev.bus.Queue(revealDoraEv);
                        isRevealed = true;
                    }
                    return Task.CompletedTask;
                }
                new EventListener<DiscardTileEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .CancelOn<IncreaseJunEvent>()
                    .ScopeTo(EventScope.Game);
                new EventListener<KanEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .CancelOn<IncreaseJunEvent>()
                    .ScopeTo(EventScope.Game);
                // 杠后自摸，仅当血战到底时才会翻Dora
                new EventListener<IncreaseJunEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .ScopeTo(EventScope.Game);
            }

            ev.bus.Queue(new DrawTileEvent(ev.game, ev.playerId, TileSource.Wanpai, DiscardReason.DrawRinshan));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<AddKanEvent>(ExecuteAddKan, EventPriority.Execute);
        }
    }
}