using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class KanAction : ChooseTilesAction {
        public override string name => "kan";
        public KanAction(Player player, List<GameTiles> tiles, int priorityDelta = 0) : base(player, tiles) {
            priority = ActionPriority.Kan + priorityDelta;
        }
    }
}