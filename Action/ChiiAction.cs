using RabiRiichi.Core;
using System.Collections.Generic;


namespace RabiRiichi.Action {
    public class ChiiAction : ChooseTilesAction {
        public override string name => "chii";
        public ChiiAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Chii + priorityDelta;
        }
    }
}