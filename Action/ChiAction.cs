using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Action {
    public class ChiAction : ChooseTilesAction {
        public override string name => "chi";
        public ChiAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Chi + priorityDelta;
        }
    }
}