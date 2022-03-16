using RabiRiichi.Communication;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Action {
    [RabiPrivate]
    public class KanAction : ChooseTilesAction {
        public override string name => "kan";
        public GameTile incoming;
        public KanAction(int playerId, List<GameTiles> tiles, GameTile incoming, int priorityDelta = 0) : base(playerId, tiles) {
            this.incoming = incoming;
            priority = ActionPriority.Kan + priorityDelta;
        }
    }
}