using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RabiRiichi.Action {
    public class ChooseTileActionOption : ActionOption {
        [JsonInclude]
        public readonly ActionTileInfo tile;

        public ChooseTileActionOption(GameTile tile) {
            this.tile = new ActionTileInfo(tile);
        }
    }

    public class ChooseTileAction : SingleChoiceAction {
        public override string id => "choose_tile";
        public ChooseTileAction(Player player, List<GameTile> tiles, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var tile in tiles) {
                AddOption(new ChooseTileActionOption(tile));
            }
        }
    }
}