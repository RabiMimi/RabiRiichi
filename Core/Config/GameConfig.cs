using RabiRiichi.Communication;
using RabiRiichi.Core.Setup;
using RabiRiichi.Generated.Core.Config;
using RabiRiichi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Core.Config {
  [RabiMessage]
  public class GameConfig {
    #region Config
    /// <summary> 玩家数 </summary>
    [RabiBroadcast] public int playerCount = 2;

    /// <summary> 几庄战 </summary>
    [RabiBroadcast] public int totalRound = 1;

    /// <summary> 番缚 </summary>
    [RabiBroadcast] public int minHan = 1;

    /// <summary> 初始牌山 </summary>
    public Tiles initialTiles = Tiles.All.Value;

    /// <summary> 分数线 </summary>
    [RabiBroadcast] public PointThreshold pointThreshold = new();

    /// <summary> 连庄策略 </summary>
    [RabiBroadcast] public RenchanPolicy renchanPolicy = RenchanPolicy.Default;

    /// <summary> 终局策略 </summary>
    [RabiBroadcast] public EndGamePolicy endGamePolicy = EndGamePolicy.Default;

    /// <summary> 食替策略 </summary>
    [RabiBroadcast] public KuikaePolicy kuikaePolicy = KuikaePolicy.Default;

    /// <summary> 立直策略 </summary>
    [RabiBroadcast] public RiichiPolicy riichiPolicy = RiichiPolicy.Default;

    /// <summary> 宝牌选项 </summary>
    [RabiBroadcast] public DoraOption doraOption = DoraOption.Default;

    /// <summary> 和牌选项 </summary>
    [RabiBroadcast] public AgariOption agariOption = AgariOption.Default;

    /// <summary> 计分选项 </summary>
    [RabiBroadcast] public ScoringOption scoringOption = ScoringOption.Default;

    /// <summary> 中途流局 </summary>
    [RabiBroadcast] public RyuukyokuTrigger ryuukyokuTrigger = RyuukyokuTrigger.Default;

    /// <summary> 技能点数扣减策略 </summary>
    [RabiBroadcast] public PointsDeductionPolicy pointsDeductionPolicy = PointsDeductionPolicy.SufficientPoints;

    [RabiBroadcast] public double nextRoundAckTimeout = 30.0;

    /// <summary> 摸打/鸣牌动作超时时间（秒） </summary>
    [RabiBroadcast] public double gameplayActionTimeout = 20.0;
    #endregion


    #region Helper methods
    public long HonbaPointsForOnePlayer(int honba) {
      return (pointThreshold.honbaPoints / (playerCount - 1) * honba).CeilTo100();
    }

    public long MinRiichiPoints {
      get {
        if (riichiPolicy.HasFlag(RiichiPolicy.SufficientPoints)) {
          return pointThreshold.validPointsRange[0] + pointThreshold.riichiPoints;
        }
        return riichiPolicy.HasFlag(RiichiPolicy.ValidPoints) ? pointThreshold.validPointsRange[0] : long.MinValue;
      }
    }

    public void Validate() {
      if (playerCount < 2 || playerCount > 4) {
        throw new InvalidGameConfigException(GameConfigErrorType.InvalidPlayerCount, "Player count must be between 2 and 4.");
      }
      if (totalRound < 1 || totalRound > 2) {
        throw new InvalidGameConfigException(GameConfigErrorType.InvalidTotalRound, "Total round must be 1 or 2.");
      }
      if (minHan < 1 || minHan > 13) {
        throw new InvalidGameConfigException(GameConfigErrorType.InvalidMinHan, "Min Han must be between 1 and 13.");
      }
      if (gameplayActionTimeout < 5 || gameplayActionTimeout > 3600) {
        throw new InvalidGameConfigException(GameConfigErrorType.InvalidTimeout, "Action timeout must be between 5 and 3600 seconds.");
      }

      int minTiles = playerCount * 13 + 15;
      if (initialTiles == null || initialTiles.Count < minTiles) {
        throw new InvalidGameConfigException(GameConfigErrorType.InsufficientTiles,
          "Insufficient tiles",
          new() {
            { "count", initialTiles?.Count ?? 0 },
            { "players", playerCount },
            { "min", minTiles }
          });
      }

      if (pointThreshold != null) {
        if (pointThreshold.initialPoints < 0 || pointThreshold.initialPoints > 1000000) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidInitialPoints, "Initial points must be between 0 and 1,000,000.");
        }
        if (pointThreshold.finishPoints < 0 || pointThreshold.finishPoints > 1000000) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidFinishPoints, "Finish points must be between 0 and 1,000,000.");
        }
        if (pointThreshold.riichiPoints < 0 || pointThreshold.riichiPoints > 1000000) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidRiichiPoints, "Riichi points must be between 0 and 1,000,000.");
        }
        if (pointThreshold.honbaPoints < 0 || pointThreshold.honbaPoints > 1000000) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidHonbaPoints, "Honba points must be between 0 and 1,000,000.");
        }
        if (pointThreshold.ryuukyokuPoints == null || pointThreshold.ryuukyokuPoints.Length < 2) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidRyuukyokuPoints, "Ryuukyoku points must contain 2 values.");
        }
        if (pointThreshold.ryuukyokuPoints.Any(p => p < 0 || p > 1000000)) {
          throw new InvalidGameConfigException(GameConfigErrorType.InvalidRyuukyokuPoints, "Ryuukyoku points must be between 0 and 1,000,000.");
        }
      }
    }
    #endregion

    #region For Developer Use
    /// <summary> 注册类 </summary>
    public BaseSetup setup = new RiichiSetup();

    public string[] AllowedYakus => setup?.GetStdPatterns()
      .Where(BaseSetup.IsYaku)
      .Select(t => t.Name)
      .ToArray() ?? [];

    /// <summary> 交互类 </summary>
    public IActionCenter actionCenter = null;

    /// <summary> 随机种子 </summary>
    public ulong? seed;

    public GameConfigMsg ToProto() {
      var ret = new GameConfigMsg {
        PlayerCount = playerCount,
        TotalRound = totalRound,
        MinHan = minHan,
        PointThreshold = pointThreshold.ToProto(),
        RenchanPolicy = (int)renchanPolicy,
        EndGamePolicy = (int)endGamePolicy,
        KuikaePolicy = (int)kuikaePolicy,
        RiichiPolicy = (int)riichiPolicy,
        DoraOption = (int)doraOption,
        AgariOption = (int)agariOption,
        ScoringOption = (int)scoringOption,
        RyuukyokuTrigger = (int)ryuukyokuTrigger,
        Seed = seed ?? 0,
        PointsDeductionPolicy = (int)pointsDeductionPolicy,
        NextRoundAckTimeout = nextRoundAckTimeout,
        GameplayActionTimeout = gameplayActionTimeout,
      };
      if (initialTiles != null) {
        ret.InitialTiles.AddRange(initialTiles.Select(t => (int)t.Val));
      }
      ret.AllowedYakus.AddRange(AllowedYakus);
      return ret;
    }

    public static GameConfig FromProto(GameConfigMsg msg) {
      if (msg == null)
        return new GameConfig();
      var config = new GameConfig();
      if (msg.PlayerCount > 0)
        config.playerCount = msg.PlayerCount;
      if (msg.TotalRound > 0)
        config.totalRound = msg.TotalRound;
      if (msg.MinHan > 0)
        config.minHan = msg.MinHan;
      if (msg.PointThreshold != null) {
        config.pointThreshold = new PointThreshold {
          initialPoints = msg.PointThreshold.InitialPoints,
          riichiPoints = msg.PointThreshold.RiichiPoints,
          honbaPoints = msg.PointThreshold.HonbaPoints,
          finishPoints = msg.PointThreshold.FinishPoints,
        };
        if (msg.PointThreshold.RyuukyokuPoints.Count > 0) {
          config.pointThreshold.ryuukyokuPoints = msg.PointThreshold.RyuukyokuPoints.ToArray();
        }
        if (msg.PointThreshold.ValidPointsRange.Count > 0) {
          config.pointThreshold.validPointsRange = msg.PointThreshold.ValidPointsRange.ToArray();
        }
      }
      if (msg.RenchanPolicy != 0)
        config.renchanPolicy = (RenchanPolicy)msg.RenchanPolicy;
      if (msg.EndGamePolicy != 0)
        config.endGamePolicy = (EndGamePolicy)msg.EndGamePolicy;
      if (msg.KuikaePolicy != 0)
        config.kuikaePolicy = (KuikaePolicy)msg.KuikaePolicy;
      if (msg.RiichiPolicy != 0)
        config.riichiPolicy = (RiichiPolicy)msg.RiichiPolicy;
      if (msg.DoraOption != 0)
        config.doraOption = (DoraOption)msg.DoraOption;
      if (msg.AgariOption != 0)
        config.agariOption = (AgariOption)msg.AgariOption;
      if (msg.ScoringOption != 0)
        config.scoringOption = (ScoringOption)msg.ScoringOption;
      if (msg.RyuukyokuTrigger != 0)
        config.ryuukyokuTrigger = (RyuukyokuTrigger)msg.RyuukyokuTrigger;
      if (msg.Seed != 0)
        config.seed = msg.Seed;
      if (msg.PointsDeductionPolicy != 0)
        config.pointsDeductionPolicy = (PointsDeductionPolicy)msg.PointsDeductionPolicy;
      if (msg.NextRoundAckTimeout > 0)
        config.nextRoundAckTimeout = msg.NextRoundAckTimeout;
      if (msg.GameplayActionTimeout > 0)
        config.gameplayActionTimeout = msg.GameplayActionTimeout;
      if (msg.InitialTiles.Count > 0) {
        config.initialTiles = new Tiles(msg.InitialTiles.Select(t => new Tile((byte)t)));
      } else {
        config.initialTiles = Tiles.All.Value;
      }
      return config;
    }

    #endregion
  }

  public enum GameConfigErrorType {
    InvalidPlayerCount,
    InvalidTotalRound,
    InvalidMinHan,
    InvalidTimeout,
    InsufficientTiles,
    InvalidInitialPoints,
    InvalidFinishPoints,
    InvalidRiichiPoints,
    InvalidHonbaPoints,
    InvalidRyuukyokuPoints,
    InvalidPointsRange,
  }

  public class InvalidGameConfigException(GameConfigErrorType errorType, string message, Dictionary<string, object> parameters = null) : Exception(message) {
    public GameConfigErrorType ErrorType { get; } = errorType;
    public Dictionary<string, object> Parameters { get; } = parameters ?? [];
  }
}