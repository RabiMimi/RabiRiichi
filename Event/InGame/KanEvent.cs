using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 加杠/暗杠/大明杠
    /// </summary>
    public class KanEvent : PlayerEvent {
        public override string name => "kan";
        #region Request
        [RabiBroadcast] public readonly Kan kan;
        [RabiBroadcast] public readonly TileSource kanSource;
        public readonly GameTile incoming;
        #endregion

        #region Response
        [RabiBroadcast] public readonly MultiPlayerInquiry inquiry;
        #endregion

        public KanEvent(Game game, int playerId, Kan kan, GameTile incoming) : base(game, playerId) {
            this.kan = kan;
            this.incoming = incoming;
            this.kanSource = kan.KanSource;
            this.inquiry = new MultiPlayerInquiry(game.info);
        }
    }
}