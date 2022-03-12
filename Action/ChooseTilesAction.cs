using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action {
    public class ActionTileInfo : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public readonly string tile;
        [RabiBroadcast] public readonly int from;
        [RabiBroadcast] public readonly int player;
        [RabiBroadcast] public readonly string source;

        public ActionTileInfo(GameTile tile) {
            this.tile = tile.ToString();
            from = tile.fromPlayer?.id ?? -1;
            player = tile.player?.id ?? -1;
            source = tile.source.ToString();
        }
    }

    public class ChooseTilesActionOption : ActionOption {

        [RabiBroadcast] public readonly List<ActionTileInfo> tiles;

        public ChooseTilesActionOption(GameTiles tiles) {
            this.tiles = tiles.Select(tile => new ActionTileInfo(tile)).ToList();
        }
    }

    public class ChooseTilesAction : SingleChoiceAction {
        public override string name => "choose_tiles";
        public ChooseTilesAction(Player player, List<GameTiles> tiles, int priorityDelta = 0) : base(player) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var gr in tiles) {
                AddOption(new ChooseTilesActionOption(gr));
            }
        }
    }
}