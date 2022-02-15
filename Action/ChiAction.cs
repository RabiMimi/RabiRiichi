using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class ChiAction : ChooseTilesAction {
        public override int Priority => 3000;
        public ChiAction(Player player, List<GameTiles> tiles) : base(player, tiles) {}
    }
}