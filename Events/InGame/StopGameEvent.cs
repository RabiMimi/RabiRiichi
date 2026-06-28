using RabiRiichi.Communication;
using System.Collections.Generic;


namespace RabiRiichi.Events.InGame {
  public class StopGameEvent(EventBase parent) : EventBase(parent) {
    public override string name => "stop_game";

    [RabiBroadcast] public readonly List<long> endGamePoints = [];
  }
}