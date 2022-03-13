using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Action {

    public class RiichiAction : ChooseTileAction {
        public override string name => "riichi";
        public RiichiAction(int playerId, List<GameTile> tiles, int priorityDelta = 0) : base(playerId, tiles) {
            priority = ActionPriority.Riichi + priorityDelta;
        }

        public override Task OnResponse(int response) {
            throw new System.NotImplementedException();
        }
    }
}