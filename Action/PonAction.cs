using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Action {
    public class PonAction : ChooseTilesAction {
        public override string name => "pon";
        public PonAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Pon + priorityDelta;
        }
    }
}