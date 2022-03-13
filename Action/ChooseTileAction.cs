using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action {
    public class ChooseTileActionOption : ActionOption {
        [RabiBroadcast] public readonly ActionTileInfo tile;

        public ChooseTileActionOption(GameTile tile) {
            this.tile = new ActionTileInfo(tile);
        }
    }

    public abstract class ChooseTileAction : SingleChoiceAction {
        public ChooseTileAction(int playerId, List<GameTile> tiles, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var tile in tiles) {
                AddOption(new ChooseTileActionOption(tile));
            }
        }
    }
}