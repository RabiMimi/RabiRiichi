using System;

namespace RabiRiichi.Core.Config {
    /// <summary> 食替检测 </summary>
    [Flags]
    public enum KuikaePolicy {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 禁止现物食替 </summary>
        Genbutsu = 1 << 0,
        /// <summary> 禁止筋食替 </summary>
        Suji = 1 << 1,
        /// <summary> 所有 </summary>
        All = Genbutsu | Suji,
        /// <summary> 默认值 </summary>
        Default = All,
    }

    /// <summary> 立直要求 </summary>
    [Flags]
    public enum RiichiPolicy {
        /// <summary> 立直无特殊要求 </summary>
        None = 0,
        /// <summary> 禁止立直后分数为负 </summary>
        SufficientPoints = 1 << 0,
        /// <summary> 禁止立直时分数为负 </summary>
        NonNegativePoints = 1 << 1,
        /// <summary> 禁止牌山剩余牌数小于玩家数时立直 </summary>
        SufficientTiles = 1 << 2,
        /// <summary> 默认值 </summary>
        Default = SufficientPoints | SufficientTiles,
        /// <summary> 所有 </summary>
        All = SufficientPoints | SufficientTiles | NonNegativePoints,
    }
}