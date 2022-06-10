using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;

namespace RabiRiichi.Actions {
    public abstract class AgariAction : ConfirmAction {
        public readonly AgariInfo agariInfo;
        public readonly GameTile incoming;
        public AgariAction(int playerId, AgariInfo agariInfo, GameTile incoming, int priorityDelta = 0) : base(playerId) {
            this.agariInfo = agariInfo;
            this.incoming = incoming;
            priority = ActionPriority.Ron + priorityDelta;
        }
    }

    public class RonAction : AgariAction {
        public override string name => "ron";
        public RonAction(AgariInfo agariInfo, GameTile incoming, int priorityDelta = 0)
            : base(agariInfo.playerId, agariInfo, incoming, priorityDelta) { }
        public RonAction(int playerId, ScoreStorage scores, GameTile incoming, int priorityDelta = 0)
            : base(playerId, new AgariInfo(playerId, scores), incoming, priorityDelta) { }
    }

    public class TsumoAction : AgariAction {
        public override string name => "tsumo";
        public TsumoAction(AgariInfo agariInfo, GameTile incoming, int priorityDelta = 0)
            : base(agariInfo.playerId, agariInfo, incoming, priorityDelta) { }
        public TsumoAction(int playerId, ScoreStorage scores, GameTile incoming, int priorityDelta = 0)
            : this(new AgariInfo(playerId, scores), incoming, priorityDelta) { }
    }
}