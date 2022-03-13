using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    /// <summary>
    /// 加杠或暗杠
    /// </summary>
    public class KaAnKanEvent : PlayerEvent {
        public override string name => "kakan_or_ankan";
        #region Request
        [RabiBroadcast] public readonly Kan kan;
        public bool isAnKan => kan.IsClose;
        #endregion
        public KaAnKanEvent(Game game, int playerId, Kan kan) : base(game, playerId) {
            this.kan = kan;
        }
    }
}