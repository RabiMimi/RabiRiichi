using RabiRiichi.Core;
using RabiRiichi.Generated.Actions;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
    public class PonAction : ChooseTilesAction {
        public override string name => "pon";
        public PonAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Pon + priorityDelta;
        }

        public PonActionMsg ToProto() {
            var ret = new PonActionMsg();
            ret.TileGroups.AddRange(options.Select(o =>
                MenLike.From(((ChooseTilesActionOption)o).tiles).ToProto()));
            return ret;
        }
    }
}