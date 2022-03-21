using RabiRiichi.Communication;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class ChooseTilesActionOption : ActionOption {
        [RabiBroadcast] public readonly GameTiles tiles;

        public ChooseTilesActionOption(GameTiles tiles) {
            this.tiles = tiles;
        }
    }

    public abstract class ChooseTilesAction : SingleChoiceAction {
        public ChooseTilesAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var gr in tiles) {
                AddOption(new ChooseTilesActionOption(gr));
            }
        }
    }
}