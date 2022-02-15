using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class KanAction : ChooseTilesAction {
        public override int Priority => 5000;
        public KanAction(Player player, List<GameTiles> tiles) : base(player, tiles) {}
    }
}