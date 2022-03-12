using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {

    public class RiichiAction : ChooseTileAction {
        public override string name => "riichi";
        public RiichiAction(Player player, List<GameTile> tiles, int priorityDelta = 0) : base(player, tiles) {
            priority = ActionPriority.Riichi + priorityDelta;
        }
    }
}