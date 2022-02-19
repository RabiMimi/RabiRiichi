using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public abstract class ChooseTilesAction : ChoiceAction<GameTiles> {
        public ChooseTilesAction(Player player, List<GameTiles> tiles) : base(player, tiles) {}
    }
}