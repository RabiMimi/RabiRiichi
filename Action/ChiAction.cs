using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Action {
    public class ChiiAction : ChooseTilesAction {
        public override string name => "chii";
        public ChiiAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Chii + priorityDelta;
        }
    }
}