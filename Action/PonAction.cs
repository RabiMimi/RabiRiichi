using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class PonAction : ChooseTilesAction {
        public override string name => "pon";
        public PonAction(Player player, List<GameTiles> tiles, int priorityDelta = 0) : base(player, tiles) {
            priority = ActionPriority.Pon + priorityDelta;
        }
    }
}