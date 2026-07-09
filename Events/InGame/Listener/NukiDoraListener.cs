using RabiRiichi.Actions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
  public static class NukiDoraListener {
    public static Task ExecuteNukiDora(NukiDoraEvent ev) {
      var hand = ev.player.hand;
      // 先把北移出手牌，使抢拔北窗口期间手牌数保持13。
      if (hand.pendingTile != null && hand.pendingTile.traceId == ev.incoming.traceId) {
        hand.pendingTile = null;
      } else {
        hand.Remove(ev.incoming);
      }

      // 抢拔北视同抢槓，但本身不计役（无役不能抢），因此用 Pretend 而非 Chankan
      // 作为弃牌原因，避免误加搶槓役。
      using var _ = ev.incoming.Freeze();
      ev.incoming.discardInfo = new DiscardInfo(ev.player, DiscardReason.Pretend, ev.game.info.timeStamp.Next);
      ev.incoming.source = TileSource.Discard;

      if (ev.game.TryGet<NukiChankanResolver>(out var resolver)) {
        resolver.Resolve(ev.player, ev.incoming, ev.waitEvent.inquiry);
      }
      ev.waitEvent.timeout = TimeSpan.FromSeconds(ev.game.config.gameplayActionTimeout);
      ev.waitEvent.inquiry.AddHandler<RonAction>((action) => {
        // 抢拔北是荣和，不是自摸
        ev.waitEvent.eventBuilder.AddAgari(ev.waitEvent, ev.playerId, ev.incoming, false, action.agariInfo);
      });
      ev.Q.Queue(ev.waitEvent);
      ev.waitEvent.OnFinish += () => {
        // 若被抢拔北（有人荣和），拔北不成立
        if (ev.waitEvent.responseEvents.Any(e => e is AgariEvent)) {
          return;
        }
        ev.Q.Queue(new AddNukiDoraEvent(ev));
      };
      return Task.CompletedTask;
    }

    public static Task ExecuteAddNukiDora(AddNukiDoraEvent ev) {
      // 北已在抢拔北窗口开始时移出手牌，这里正式放入拔北宝牌区。
      ev.player.hand.Nuki(ev.incoming);

      // 拔北后从岭上补一张牌（不翻新的宝牌指示牌）
      ev.Q.Queue(new IncreaseJunEvent(ev, ev.playerId));
      ev.Q.Queue(new DrawTileEvent(ev, ev.playerId, TileSource.Wanpai));
      return Task.CompletedTask;
    }

    public static void Register(EventBus eventBus) {
      eventBus.Subscribe<NukiDoraEvent>(ExecuteNukiDora, EventPriority.Execute);
      eventBus.Subscribe<AddNukiDoraEvent>(ExecuteAddNukiDora, EventPriority.Execute);
    }
  }
}
