using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Utils;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class AddKanListener {
        private static Task ExecuteAddKan(AddKanEvent ev) {
            // Remove fake discard info
            ev.incoming.discardInfo = null;
            if (ev.kanSource == TileSource.Ankan || ev.kanSource == TileSource.Daiminkan) {
                ev.player.hand.AddKan(ev.kan);
            } else if (ev.kanSource == TileSource.Kakan) {
                ev.player.hand.Kakan(ev.kan);
            } else {
                return Task.CompletedTask;
            }

            // Check if should reveal dora immediately
            bool instantReveal = ev.game.config.doraOption.HasAnyFlag(ev.kanSource switch {
                TileSource.Ankan => DoraOption.InstantRevealAfterAnkan,
                TileSource.Kakan => DoraOption.InstantRevealAfterKakan,
                TileSource.Daiminkan => DoraOption.InstantRevealAfterDaiminkan,
                _ => throw new ArgumentException($"Invalid kan source: {ev.kanSource}")
            });

            if (instantReveal) {
                ev.Q.Queue(new RevealDoraEvent(ev, ev.playerId));
            } else {
                var revealDoraEv = new RevealDoraEvent(ev, ev.playerId);
                bool isRevealed = false;
                Task DelayRevealDora(EventBase _) {
                    if (!isRevealed) {
                        ev.Q.Queue(revealDoraEv);
                        isRevealed = true;
                    }
                    return Task.CompletedTask;
                }
                new EventListener<DiscardTileEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .CancelOn<NextPlayerEvent>()
                    .ScopeTo(EventScope.Game);
                new EventListener<KanEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .CancelOn<NextPlayerEvent>()
                    .ScopeTo(EventScope.Game);
                // 杠后自摸，仅当血战到底时才会翻Dora
                new EventListener<NextPlayerEvent>(ev.bus)
                    .EarlyPrepare(DelayRevealDora, 1)
                    .ScopeTo(EventScope.Game);
            }

            ev.Q.Queue(new IncreaseJunEvent(ev, ev.playerId));
            ev.Q.Queue(new DrawTileEvent(ev, ev.playerId, TileSource.Wanpai));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<AddKanEvent>(ExecuteAddKan, EventPriority.Execute);
        }
    }
}