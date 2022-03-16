using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public class ClaimTileEvent : BroadcastPlayerEvent {
        public override string name => "claim_tile";

        #region Request
        [RabiBroadcast] public GameTile tile;
        [RabiBroadcast] public MenLike group;
        #endregion

        #region Response
        #endregion

        public ClaimTileEvent(Game game, int playerId, MenLike group, GameTile tile) : base(game, playerId) {
            this.group = group;
            this.tile = tile;
        }
    }
}
