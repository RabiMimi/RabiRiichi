using RabiRiichi.Communication;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action {
    public class ActionTileInfo : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        public GameTile gameTile;
        [RabiBroadcast] public readonly string tile;
        [RabiBroadcast] public readonly int from;
        [RabiBroadcast] public readonly int player;
        [RabiBroadcast] public readonly string source;

        public ActionTileInfo(GameTile tile) {
            gameTile = tile;
            this.tile = tile.ToString();
            from = tile.fromPlayerId ?? -1;
            player = tile.playerId ?? -1;
            source = tile.source.ToString();
        }
    }

    public class ChooseTilesActionOption : ActionOption {

        [RabiBroadcast] public readonly List<ActionTileInfo> tiles;
        public readonly GameTiles gameTiles;

        public ChooseTilesActionOption(GameTiles tiles) {
            gameTiles = tiles;
            this.tiles = tiles.Select(tile => new ActionTileInfo(tile)).ToList();
        }
    }

    public abstract class ChooseTilesAction : SingleChoiceAction {
        public ChooseTilesAction(int playerId, List<GameTiles> tiles, int priorityDelta = 0) : base(playerId) {
            priority = ActionPriority.ChooseTile + priorityDelta;
            foreach (var gr in tiles) {
                AddOption(new ChooseTilesActionOption(gr));
            }
        }
    }
}