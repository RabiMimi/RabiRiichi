﻿using RabiRiichi.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DealHandListener {
        public static Task DealHand(DealHandEvent e) {
            var player = e.player;
            e.tiles = new GameTiles(e.game.wall.Draw(e.count));
            foreach (var tile in e.tiles) {
                player.hand.Add(tile);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<DealHandEvent>(DealHand, EventPriority.Execute);
        }
    }
}
