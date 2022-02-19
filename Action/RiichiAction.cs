using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {

    public class RiichiAction : ChooseTileAction {
        public RiichiAction(Player player, List<GameTile> tiles) : base(player, tiles) {}
    }
}