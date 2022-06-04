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
        /// <summary> 所有 </summary>
        All = SufficientPoints | SufficientTiles | NonNegativePoints,
        /// <summary> 默认值 </summary>
        Default = SufficientPoints | SufficientTiles,
    }

    /// <summary> 流局方式 </summary>
    [Flags]
    public enum RyuukyokuTrigger {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 四风连打 </summary>
        SuufonRenda = 1 << 0,
        /// <summary> 九种九牌 </summary>
        KyuushuKyuuhai = 1 << 1,
        /// <summary> 四家立直 </summary>
        SuuchaRiichi = 1 << 2,
        /// <summary> 三家荣和 </summary>
        Sanchahou = 1 << 3,
        /// <summary> 四杠散了 </summary>
        SuukanSanra = 1 << 4,
        /// <summary> 所有 </summary>
        All = SuufonRenda | KyuushuKyuuhai | SuuchaRiichi | Sanchahou | SuukanSanra,
        /// <summary> 默认值 </summary>
        Default = All,
    }

    /// <summary> 对局继续选项 </summary>
    [Flags]
    public enum ContinuationOption {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 庄家和了时连庄 </summary>
        RenchanOnDealerWin = 1 << 0,
        /// <summary> 庄家流局听牌时连庄 </summary>
        RenchanOnDealerTenpai = 1 << 1,
        /// <summary> 和牌/流局结算时，可以击飞 </summary>
        /// TODO: Implement
        TerminateOnNegativeScore = 1 << 2,
        /// <summary> 分数为负时立即击飞 </summary>
        /// TODO: Implement
        InstantTerminateOnNegativeScore = 1 << 3,
        /// <summary> 所有 </summary>
        All = RenchanOnDealerWin | RenchanOnDealerTenpai | TerminateOnNegativeScore | InstantTerminateOnNegativeScore,
        /// <summary> 默认值 </summary>
        Default = RenchanOnDealerWin | RenchanOnDealerTenpai | TerminateOnNegativeScore,
    }
}