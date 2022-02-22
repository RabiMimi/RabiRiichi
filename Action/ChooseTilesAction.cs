using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class ActionTileInfo {
        [JsonInclude]
        public readonly string tile;
        [JsonInclude]
        public readonly int fromPlayer;
        [JsonInclude]
        public readonly int player;

        public ActionTileInfo(GameTile tile) {
            this.tile = tile.ToString();
            fromPlayer = tile.fromPlayer?.id ?? -1;
            player = tile.player?.id ?? -1;
        }
    }

    public class ChooseTilesActionOption : ActionOption {
        public override string id => "choose_tiles";

        [JsonInclude]
        public readonly List<ActionTileInfo> tiles;

        public ChooseTilesActionOption(GameTiles tiles) {
            this.tiles = tiles.Select(tile => new ActionTileInfo(tile)).ToList();
        }
    }

    public abstract class ChooseTilesAction : ChoiceAction {
        public ChooseTilesAction(Player player, List<GameTiles> tiles) : base(player) { }
    }
}