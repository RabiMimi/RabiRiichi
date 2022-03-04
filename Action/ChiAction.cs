using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class ChiAction : ChooseTilesAction {
        public override string id => "chi";
        public override int priority => ActionPriority.Chi;
        public ChiAction(Player player, List<GameTiles> tiles) : base(player, tiles) { }
    }
}