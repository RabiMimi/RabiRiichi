using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Actions {

    public class RiichiAction : PlayTileAction {
        public override string name => "riichi";

        public RiichiAction(int playerId, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(playerId, tiles, defaultTile) {
            priority = ActionPriority.Riichi + priorityDelta;
        }
    }
}