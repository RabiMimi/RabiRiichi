using RabiRiichi.Core;
using RabiRiichi.Generated.Actions;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
    public class PlayTileAction : ChooseTileAction {
        public override string name => "play_tile";
        public readonly GameTile defaultTile;

        public PlayTileAction(int playerId, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(playerId, tiles, priorityDelta) {
            this.defaultTile = defaultTile;
            int index = tiles.IndexOf(defaultTile);
            if (ValidateResponse(index)) {
                response = index;
            }
        }

        public PlayTileActionMsg ToProto() {
            var ret = new PlayTileActionMsg();
            ret.Tiles.AddRange(options.Select(
                o => ((ChooseTileActionOption)o).tile.ToProto()));
            return ret;
        }
    }
}