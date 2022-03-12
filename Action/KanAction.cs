using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class KanAction : ChooseTilesAction {
        public override string name => "kan";
        public KanAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Kan + priorityDelta;
        }
    }
}