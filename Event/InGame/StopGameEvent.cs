using RabiRiichi.Communication;
using System.Collections.Generic;


namespace RabiRiichi.Event.InGame {
    public class StopGameEvent : EventBase {
        public override string name => "stop_game";

        [RabiBroadcast] public readonly List<int> endGamePoints;

        public StopGameEvent(EventBase parent) : base(parent) {
            endGamePoints = new List<int>();
        }
    }
}
