using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace RabiRiichi.Action {
    public class ActionTileInfo {
        [JsonInclude]
        public readonly string tile;
        [JsonInclude]
        public readonly int from;
        [JsonInclude]
        public readonly int player;
        [JsonInclude]
        public readonly string source;

        public ActionTileInfo(GameTile tile) {
            this.tile = tile.ToString();
            from = tile.fromPlayer?.id ?? -1;
            player = tile.player?.id ?? -1;
            source = tile.source.ToString();
        }
    }

    public class ChooseTilesActionOption : ActionOption {

        [JsonInclude]
        public readonly List<ActionTileInfo> tiles;

        public ChooseTilesActionOption(GameTiles tiles) {
            this.tiles = tiles.Select(tile => new ActionTileInfo(tile)).ToList();
        }
    }

    public class ChooseTilesAction : ChoiceAction<int> {
        public override string id => "choose_tiles";
        public ChooseTilesAction(Player player, List<GameTiles> tiles, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var gr in tiles) {
                AddOption(new ChooseTilesActionOption(gr));
            }
        }
    }
}