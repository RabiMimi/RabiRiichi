using RabiRiichi.Core;
using System.Collections.Generic;


namespace RabiRiichi.Action {
    public class PonAction : ChooseTilesAction {
        public override string name => "pon";
        public PonAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Pon + priorityDelta;
        }
    }
}