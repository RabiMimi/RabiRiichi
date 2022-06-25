using RabiRiichi.Core;
using RabiRiichi.Generated.Actions;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
    public class ChiiAction : ChooseTilesAction {
        public override string name => "chii";
        public ChiiAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Chii + priorityDelta;
        }

        public ChiiActionMsg ToProto() {
            var ret = new ChiiActionMsg();
            ret.TileGroups.AddRange(options.Select(o =>
                MenLike.From(((ChooseTilesActionOption)o).tiles).ToProto()));
            return ret;
        }
    }
}