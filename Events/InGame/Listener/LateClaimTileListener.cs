using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class LateClaimTileListener {
        public static IEnumerable<Tile> GetForbiddenTiles(GameConfig config, MenLike men, GameTile incoming) {
            if (config.kuikaePolicy.HasAnyFlag(KuikaePolicy.Genbutsu)) {
                yield return incoming.tile.WithoutDora;
            }
            if (config.kuikaePolicy.HasAnyFlag(KuikaePolicy.Suji) && men is Shun) {
                if (incoming == men[0]) {
                    var tile = men[^1].tile.Next;
                    if (tile.IsValid) {
                        yield return tile;
                    }
                }
                if (incoming == men[^1]) {
                    var tile = men[0].tile.Prev;
                    if (tile.IsValid) {
                        yield return tile;
                    }
                }
            }
        }

        public static Task PrepareLateClaimTile(LateClaimTileEvent ev) {
            var parent = (ClaimTileEvent)ev.parent;
            ev.forbidden.AddRange(GetForbiddenTiles(ev.game.config, parent.group, parent.tile));
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