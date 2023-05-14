using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Actions {
    public class ChooseTileActionOption : ActionOption {
        [RabiBroadcast] public readonly GameTile tile;

        public ChooseTileActionOption(GameTile tile) {
            this.tile = tile;
        }
    }

    public abstract class ChooseTileAction : SingleChoiceAction<ChooseTileActionOption> {
        public ChooseTileAction(int playerId, List<GameTile> tiles, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var tile in tiles) {
                AddOption(new ChooseTileActionOption(tile));
            }
        }
    }
}