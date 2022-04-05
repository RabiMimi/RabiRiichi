using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;


namespace RabiRiichi.Action {
    [RabiPrivate]
    public class KanAction : ChooseTilesAction {
        public override string name => "kan";
        public KanAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Kan + priorityDelta;
        }
    }
}