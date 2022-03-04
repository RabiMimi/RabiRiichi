using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {

    public class RiichiAction : ChooseTileAction {
        public override string id => "riichi";
        public override int priority => ActionPriority.Riichi;
        public RiichiAction(Player player, List<GameTile> tiles) : base(player, tiles) { }
    }
}