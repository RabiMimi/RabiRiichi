using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class PonAction : ChooseTilesAction {
        public override int Priority => 4000;
        public PonAction(Player player, List<GameTiles> tiles) : base(player, tiles) {}
    }
}