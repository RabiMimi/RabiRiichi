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
        /// <summary> 终局流局时必定连庄 </summary>
        EndGameRyuukyoku = 1 << 2,
        /// <summary> 中途流局时连庄 </summary>
        MidGameRyuukyoku = 1 << 3,
        /// <summary> 所有 </summary>
        All = DealerWin | DealerTenpai | EndGameRyuukyoku | MidGameRyuukyoku,
        /// <summary> 默认值 </summary>
        Default = DealerWin | DealerTenpai | MidGameRyuukyoku,
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
        /// <summary> 不满足终局条件时，南/西入 </summary>
        ExtendedRound = 1 << 4,
        /// <summary> 所有 </summary>
        All = PointsOutOfRange | InstantPointsOutOfRange | DealerTenpai | DealerAgari | ExtendedRound,
        /// <summary> 默认值 </summary>
        Default = PointsOutOfRange | DealerTenpai | DealerAgari | ExtendedRound,
    }

    /// <summary> 宝牌选项 </summary>
    [Flags]
    public enum DoraOption {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 初始表宝牌 </summary>
        InitialDora = 1 << 0,
        /// <summary> 初始里宝牌 </summary>
        InitialUradora = 1 << 1,
        /// <summary> 杠表宝牌 </summary>
        KanDora = 1 << 2,
        /// <summary> 杠里宝牌 </summary>
        KanUradora = 1 << 3,
        /// <summary> 里宝牌 </summary>
        Uradora = InitialUradora | KanUradora,
        /// <summary> 表宝牌 </summary>
        Dora = InitialDora | KanDora,
        /// <summary> 大明杠宝牌即开 </summary>
        InstantRevealAfterDaiminkan = 1 << 4,
        /// <summary> 加杠宝牌即开 </summary>
        InstantRevealAfterKakan = 1 << 5,
        /// <summary> 暗杠宝牌即开 </summary>
        InstantRevealAfterAnkan = 1 << 6,
        /// <summary> 所有 </summary>
        All = Dora | Uradora | InstantRevealAfterDaiminkan | InstantRevealAfterKakan | InstantRevealAfterAnkan,
        /// <summary> 默认值 </summary>
        Default = Dora | Uradora | InstantRevealAfterAnkan,
    }

    /// <summary> 和牌选项 </summary>
    [Flags]
    public enum AgariOption {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 允许食断 </summary>
        Kuitan = 1 << 0,
        /// <summary> 允许包牌 </summary>
        Pao = 1 << 1,
        /// <summary> 启用流局满贯 </summary>
        NagashiMangan = 1 << 2,
        /// <summary> 启用头跳 </summary>
        FirstWinner = 1 << 3,
        /// <summary> 所有 </summary>
        All = Kuitan | Pao | NagashiMangan | FirstWinner,
        /// <summary> 默认值 </summary>
        Default = Kuitan | Pao | NagashiMangan,
    }

    /// <summary> 计分选项 </summary>
    [Flags]
    public enum ScoringOption {
        /// <summary> 无 </summary>
        None = 0,
        /// <summary> 启用切上满贯 </summary>
        KiriageMangan = 1 << 0,
        /// <summary>
        /// 启用役满。若不启用役满，则以下役满相关选项全部无效，且计分时采用青天井规则
        /// </summary>
        Yakuman = 1 << 1,
        /// <summary> 启用多倍役满 </summary>
        MultipleYakuman = 1 << 2,
        /// <summary> 启用累计役满 </summary>
        KazoeYakuman = 1 << 3,
        /// <summary> 青天井 </summary>
        Aotenjou = None,
        /// <summary> 所有 </summary>
        All = KiriageMangan | Yakuman | MultipleYakuman | KazoeYakuman,
        /// <summary> 默认值 </summary>
        Default = Yakuman | MultipleYakuman | KazoeYakuman,
    }
}