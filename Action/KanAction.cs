using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class KanAction : ChooseTilesAction {
        public override string id => "kan";
        public override int priority => ActionPriority.Kan;
        public KanAction(Player player, List<GameTiles> tiles) : base(player, tiles) { }
    }
}