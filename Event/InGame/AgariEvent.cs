using RabiRiichi.Interact;
using RabiRiichi.Riichi;

namespace RabiRiichi.Event.InGame {
    public abstract class AgariEvent : BroadcastPlayerEvent {
        #region Request
        [RabiBroadcast] public readonly GameTile incoming;
        #endregion

        public AgariEvent(Game game, int playerId, GameTile incoming) : base(game, playerId) {
            this.incoming = incoming;
        }

        public static AgariEvent From(Game game, int playerId, GameTile incoming) {
            if (incoming.IsTsumo) {
                return new TsumoEvent(game, playerId, incoming);
            } else {
                return new RonEvent(game, playerId, incoming);
            }
        }
    }

    public class RonEvent : AgariEvent {
        public override string name => "ron";

        public RonEvent(Game game, int playerId, GameTile incoming) : base(game, playerId, incoming) { }
    }

    public class TsumoEvent : AgariEvent {
        public override string name => "tsumo";

        public TsumoEvent(Game game, int playerId, GameTile incoming) : base(game, playerId, incoming) { }
    }
}