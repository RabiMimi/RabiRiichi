using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {

    public class ChooseTileAction : ChoiceAction<GameTile> {
        public override int Priority => 1000;
        public ChooseTileAction(Player player, List<GameTile> tiles) : base(player, tiles) {}
    }
}