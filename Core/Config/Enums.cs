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
        /// <summary> 禁止立直后被飞 </summary>
        SufficientPoints = 1 << 0,
        /// <summary> 禁止被飞状态下立直 </summary>
        ValidPoints = 1 << 1,
        /// <summary> 禁止牌山剩余牌数小于玩家数时立直 </summary>
        SufficientTiles = 1 << 2,
        /// <summary> 所有 </summary>
        All = SufficientPoints | SufficientTiles | ValidPoints,
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

    /// <summary> 连庄策略 </summary>
    [Flags]
    public enum RenchanPolicy {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 庄家和了时连庄 </summary>
        DealerWin = 1 << 0,
        /// <summary> 庄家流局听牌时连庄 </summary>
        DealerTenpai = 1 << 1,
        /// <summary> 流局时连庄 </summary>
        Ryuukyoku = 1 << 2,
        /// <summary> 所有 </summary>
        All = DealerWin | DealerTenpai | Ryuukyoku,
        /// <summary> 默认值 </summary>
        Default = DealerWin | DealerTenpai,
    }

    /// <summary> 终局策略 </summary>
    [Flags]
    public enum EndGamePolicy {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 和牌/流局结算时若分数超出天边，结束游戏 </summary>
        PointsOutOfRange = 1 << 0,
        /// <summary> 分数超出天边时立即结束游戏 </summary>
        InstantPointsOutOfRange = 1 << 1,
        /// <summary> 若庄家一位听牌且分数达标，结束游戏 </summary>
        DealerTenpai = 1 << 2,
        /// <summary> 若庄家一位和牌且分数达标，结束游戏 </summary>
        DealerAgari = 1 << 3,
        /// <summary> 所有 </summary>
        All = PointsOutOfRange | InstantPointsOutOfRange | DealerTenpai | DealerAgari,
        /// <summary> 默认值 </summary>
        Default = PointsOutOfRange | DealerTenpai | DealerAgari,
    }
}