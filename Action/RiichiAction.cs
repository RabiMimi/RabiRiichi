using RabiRiichi.Riichi;
using System.Collections.Generic;


namespace RabiRiichi.Action {

    public class RiichiAction : PlayTileAction {
        public override string name => "riichi";

        public RiichiAction(int playerId, List<GameTile> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Riichi + priorityDelta;
        }
    }
}