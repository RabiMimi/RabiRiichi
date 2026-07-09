using RabiRiichi.Core;
using System.Collections.Generic;

namespace RabiRiichi.Actions {
  /// <summary> 拔北：从手牌中拔出一张北风放到拔北宝牌区 </summary>
  public class NukiDoraAction : ChooseTilesAction {
    public override string name => "nuki_dora";
    public NukiDoraAction(int playerId, List<List<GameTile>> tiles, int priorityDelta = 0) : base(playerId, tiles) {
      priority = ActionPriority.ChooseTile + priorityDelta;
    }
  }
}
