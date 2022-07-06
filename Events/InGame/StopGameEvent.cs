using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;
using System.Collections.Generic;


namespace RabiRiichi.Events.InGame {
    public class StopGameEvent : EventBase {
        public override string name => "stop_game";

        [RabiBroadcast] public readonly List<long> endGamePoints = new();

        public StopGameEvent(EventBase parent) : base(parent) { }
    }
}
