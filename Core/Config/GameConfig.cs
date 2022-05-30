
using RabiRiichi.Communication;
using RabiRiichi.Core.Setup;
using System;

namespace RabiRiichi.Core.Config {
    /// <summary> 立直要求 </summary>
    [Flags]
    public enum RiichiPolicy {
        /// <summary> 立直无特殊要求 </summary>
        None = 0,
        /// <summary> 禁止立直后分数为负 </summary>
        SufficientPoints = 1 << 0,
        /// <summary> 禁止立直时分数为负 </summary>
        NonNegativePoints = 1 << 1,
    }

    public class GameConfig : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        /// <summary> 玩家数 </summary>
        [RabiBroadcast] public int playerCount = 2;

        /// <summary> 番缚 </summary>
        [RabiBroadcast] public int minHan = 1;

        /// <summary> 初始点数 </summary>
        [RabiBroadcast] public int initialPoints = 25000;

        /// <summary> 立直棒点数 </summary>
        [RabiBroadcast] public int riichiPoints = 1000;

        /// <summary> 场棒点数 </summary>
        [RabiBroadcast] public int honbaPoints = 100;

        /// <summary> (西)入点数 </summary>
        [RabiBroadcast] public int finishPoints = 30000;

        /// <summary> 食断 </summary>
        [RabiBroadcast] public bool allowKuitan = true;

        /// <summary> 包牌 </summary>
        [RabiBroadcast] public bool allowPao = true;

        /// <summary> 现物食替 </summary>
        [RabiBroadcast] public bool allowGenbutsuKuikae = false;

        /// <summary> 筋食替 </summary>
        [RabiBroadcast] public bool allowSujiKuikae = false;

        /// <summary> 几庄战 </summary>
        [RabiBroadcast] public int totalRound = 1;

        /// <summary> 注册类 </summary>
        public BaseSetup setup = new RiichiSetup();

        /// <summary> 交互类 </summary>
        public IActionCenter actionCenter = null;

        /// <summary> 随机种子 </summary>
        public ulong? seed;
    }
}