using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Generated.Events.InGame;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Events.InGame {
  /// <summary>
  /// 所有流局事件继承该类
  /// </summary>
  public abstract class RyuukyokuEvent(EventBase parent) : EventBase(parent) {

    #region Request
    [RabiBroadcast] public readonly ScoreTransferList scoreChange = [];

    #endregion

    public void AddScoreTransfer(int from, int to, long points, ScoreTransferReason reason) {
      scoreChange.Add(new ScoreTransfer(from, to, points, reason));
    }
  }

  public class EndGameRyuukyokuEvent(EventBase parent) : RyuukyokuEvent(parent) {
    public override string name => "end_game_ryuukyoku";

    #region Response
    [RabiBroadcast] public int[] remainingPlayers = [];
    [RabiBroadcast] public int[] nagashiManganPlayers = [];
    [RabiBroadcast] public int[] tenpaiPlayers = [];
    [RabiBroadcast] public readonly List<GameTile> revealedTiles = [];

    #endregion
  }

  public abstract class MidGameRyuukyokuEvent(EventBase parent) : RyuukyokuEvent(parent) {
  }

  public class SuufonRenda(EventBase parent) : MidGameRyuukyokuEvent(parent) {
    public override string name => "suufon_renda";
  }

  public class KyuushuKyuuhai(EventBase parent) : MidGameRyuukyokuEvent(parent) {
    public override string name => "kyuushu_kyuuhai";
  }

  public class SuuchaRiichi(EventBase parent) : MidGameRyuukyokuEvent(parent) {
    public override string name => "suucha_riichi";
  }

  public class Sanchahou(EventBase parent) : MidGameRyuukyokuEvent(parent) {
    public override string name => "triple_ron";
  }

  public class SuukanSanra(EventBase parent) : MidGameRyuukyokuEvent(parent) {
    public override string name => "suukan_sanra";
  }
}