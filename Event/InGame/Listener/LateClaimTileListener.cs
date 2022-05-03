using RabiRiichi.Action.Resolver;
using RabiRiichi.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class LateClaimTileListener {
        public static Task PrepareLateClaimTile(LateClaimTileEvent ev) {
            var parent = (ClaimTileEvent)ev.parent;
            if (!ev.game.config.allowGenbutsuKuikae) {
                ev.forbidden.Add(parent.tile.tile);
            }
            if (!ev.game.config.allowSujiKuikae && parent.group is Shun shun) {
                if (parent.tile == shun[0]) {
                    var tile = shun[^1].tile.Next;
                    if (tile.IsValid) {
                        ev.forbidden.Add(tile);
                    }
                }
                if (parent.tile == shun[^1]) {
                    var tile = shun[0].tile.Prev;
                    if (tile.IsValid) {
                        ev.forbidden.Add(tile);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public static Task ExecuteLateClaimTile(LateClaimTileEvent ev) {
            // Request player action
            if (!PlayTileResolver.ResolveAction(ev.player, null, ev.waitEvent.inquiry, ev.forbidden)) {
                // No tile to play, allow kuikae
                PlayTileResolver.ResolveAction(ev.player, null, ev.waitEvent.inquiry, null);
            }
            ev.waitEvent.inquiry.GetByPlayerId(ev.playerId).DisableSkip();
            DrawTileListener.AddActionHandler(ev.waitEvent, ev.reason);
            ev.Q.Queue(ev.waitEvent);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<LateClaimTileEvent>(PrepareLateClaimTile, EventPriority.Prepare);
            eventBus.Subscribe<LateClaimTileEvent>(ExecuteLateClaimTile, EventPriority.Execute);
        }
    }
}