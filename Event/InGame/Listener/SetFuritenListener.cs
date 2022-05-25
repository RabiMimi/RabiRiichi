using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetFuritenListener {
        public static Task SetFuriten(SetFuritenEvent ev) {
            if (ev is SetRiichiFuritenEvent) {
                ev.player.hand.isRiichiFuriten = ev.isFuriten;
            } else if (ev is SetTempFuritenEvent) {
                ev.player.hand.isTempFuriten = ev.isFuriten;
            } else if (ev is SetDiscardFuritenEvent) {
                ev.player.hand.isDiscardFuriten = ev.isFuriten;
            }
            return Task.CompletedTask;
        }

        public static Task OnIncreaseJun(IncreaseJunEvent ev) {
            ev.Q.Queue(new SetTempFuritenEvent(ev, ev.playerId, false));
            return Task.CompletedTask;
        }

        public static void OnDiscardOrKan(PlayerEvent ev, Tile incoming) {
            List<SetFuritenEvent> furitenEvs = new();
            foreach (var player in ev.game.players) {
                var hand = player.hand;
                var tenpai = hand.Tenpai;
                if (player.id == ev.playerId) {
                    // 弃牌的玩家听牌牌型可能改变，需要全部重新计算
                    // 理论上立直后听牌牌型不能改变，因此可以简化为只计算当前弃牌
                    // 但是考虑到无限立直的情况，这里不做该优化
                    bool discardFuriten = hand.discarded.Any(tile => tenpai.Contains(tile.tile.WithoutDora));
                    furitenEvs.Add(new SetDiscardFuritenEvent(ev, player.id, discardFuriten));
                    continue;
                }
                // 计算别的玩家的振听状态
                bool tempFuriten = tenpai.Contains(incoming.WithoutDora);
                if (tempFuriten) {
                    furitenEvs.Add(new SetTempFuritenEvent(ev, player.id, tempFuriten));
                    if (hand.riichi) {
                        furitenEvs.Add(new SetRiichiFuritenEvent(ev, player.id, tempFuriten));
                    }
                }
            }
            // 在下一个巡数增加事件发生时，更新振听（玩家不选择和牌时，才会进入振听）
            new EventListener<IncreaseJunEvent>(ev.bus)
                .EarlyAfter((incJunEv) => {
                    foreach (var furitenEv in furitenEvs) {
                        ev.Q.Queue(furitenEv);
                    }
                    return Task.CompletedTask;
                }, 1)
                .ScopeTo(EventScope.Game);
        }

        public static Task OnKan(AddKanEvent ev) {
            OnDiscardOrKan(ev, ev.incoming.tile);
            return Task.CompletedTask;
        }

        public static Task OnDiscardTile(DiscardTileEvent ev) {
            OnDiscardOrKan(ev, ev.tile.tile);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SetFuritenEvent>(SetFuriten, EventPriority.Execute);
            eventBus.Subscribe<IncreaseJunEvent>(OnIncreaseJun, EventPriority.After);
            eventBus.Subscribe<DiscardTileEvent>(OnDiscardTile, EventPriority.After);
            eventBus.Subscribe<AddKanEvent>(OnKan, EventPriority.After);
        }
    }
}