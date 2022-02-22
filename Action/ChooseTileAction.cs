using System.Collections.Generic;
using System.Text.Json.Serialization;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class ChooseTileActionOption : ActionOption {
        public override string id => "choose_tile";
        [JsonInclude]
        public readonly ActionTileInfo tile;
        public ChooseTileActionOption(GameTile tile) {
            this.tile = new ActionTileInfo(tile);
        }
    }

    public class ChooseTileAction : ChoiceAction {
        public override int Priority => 1000;
        public ChooseTileAction(Player player, List<GameTile> tiles) : base(player) {
            foreach (var tile in tiles) {
                AddOption(new ChooseTileActionOption(tile));
            }
        }
    }
}