using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class PonAction : ChooseTilesAction {
        public override string id => "pon";
        public override int priority => ActionPriority.Pon;
        public PonAction(Player player, List<GameTiles> tiles) : base(player, tiles) { }
    }
}