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
        public override int priority => ActionPriority.ChooseTile;
        public ChooseTileAction(Player player, List<GameTile> tiles) : base(player) {
            foreach (var tile in tiles) {
                AddOption(new ChooseTileActionOption(tile));
            }
        }
    }
}