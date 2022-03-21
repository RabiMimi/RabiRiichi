using RabiRiichi.Riichi;
using System.Collections.Generic;


namespace RabiRiichi.Action {
    public class PlayTileAction : ChooseTileAction {
        public override string name => "play_tile";

        public PlayTileAction(int playerId, List<GameTile> tiles, GameTile defaultTile, int priorityDelta = 0) : base(playerId, tiles, priorityDelta) {
            int index = tiles.IndexOf(defaultTile);
            if (ValidateResponse(index)) {
                response = index;
            }
        }
    }
}