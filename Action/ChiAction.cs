using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class ChiAction : ChooseTilesAction {
        public override string id => "chi";
        public ChiAction(Player player, List<GameTiles> tiles, int priorityDelta = 0) : base(player, tiles) {
            priority = ActionPriority.Chi + priorityDelta;
        }
    }
}