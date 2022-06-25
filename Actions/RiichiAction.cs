using RabiRiichi.Core;
using RabiRiichi.Generated.Actions;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {

    public class RiichiAction : PlayTileAction {
        public override string name => "riichi";

        public RiichiAction(int playerId, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(playerId, tiles, defaultTile) {
            priority = ActionPriority.Riichi + priorityDelta;
        }

        public new RiichiActionMsg ToProto() {
            var ret = new RiichiActionMsg();
            ret.Tiles.AddRange(options.Select(
                o => ((ChooseTileActionOption)o).tile.ToProto()));
            return ret;
        }
    }
}