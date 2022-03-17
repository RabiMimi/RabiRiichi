using RabiRiichi.Riichi;
using System.Collections.Generic;


namespace RabiRiichi.Action {
    public class PlayTileAction : ChooseTileAction {
        public override string name => "play_tile";

        public PlayTileAction(int playerId, List<GameTile> tiles, int priorityDelta = 0) : base(playerId, tiles, priorityDelta) { }
    }
}