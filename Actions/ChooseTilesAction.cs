using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Actions {
    public class ChooseTilesActionOption : ActionOption {
        [RabiBroadcast] public readonly List<GameTile> tiles;

        public ChooseTilesActionOption(List<GameTile> tiles) {
            this.tiles = tiles;
        }
    }

    public abstract class ChooseTilesAction : SingleChoiceAction {
        public ChooseTilesAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var gr in tiles) {
                AddOption(new ChooseTilesActionOption(gr));
            }
        }
    }
}