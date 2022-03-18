using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Pattern;
using System;


namespace RabiRiichi.Riichi.Setup {
    public class RiichiSetup : BaseSetup {
        protected override void InjectResolvers(IServiceCollection collection) {
            collection.TryAddSingleton<ChanKanResolver>();
            collection.TryAddSingleton<RyuukyokuResolver>();
            collection.TryAddSingleton<ChiiResolver>();
            collection.TryAddSingleton<PonResolver>();
            collection.TryAddSingleton<KanResolver>();
            collection.TryAddSingleton<PlayTileResolver>();
            collection.TryAddSingleton<RiichiResolver>();
            collection.TryAddSingleton<RonResolver>();
            collection.TryAddSingleton<TsumoResolver>();
            collection.TryAddSingleton<TenhouResolver>();
        }

        protected override void InitPatterns() {
            // Base patterns
            AddBasePattern<Base33332>();
            AddBasePattern<Base72>();
            AddBasePattern<Base13_1>();

            // Std patterns
            AddStdPattern<一发>();
            AddStdPattern<一杯口>();
            AddStdPattern<岭上开花>();
            AddStdPattern<平和>();
            AddStdPattern<役牌中>();
            AddStdPattern<役牌发>();
            AddStdPattern<役牌场风>();
            AddStdPattern<役牌白>();
            AddStdPattern<役牌自风>();
            AddStdPattern<断幺九>();
            AddStdPattern<枪杠>();
            AddStdPattern<河底捞鱼>();
            AddStdPattern<海底摸月>();
            AddStdPattern<立直>();
            AddStdPattern<门清自摸和>();
            AddStdPattern<纯全带幺九>();
            AddStdPattern<一气通贯>();
            AddStdPattern<三暗刻>();
            AddStdPattern<三杠子>();
            AddStdPattern<三色同刻>();
            AddStdPattern<双立直>();
            AddStdPattern<对对和>();
            AddStdPattern<混全带幺九>();
            AddStdPattern<混老头>();
            AddStdPattern<四暗刻>();
            AddStdPattern<四杠子>();
            AddStdPattern<清老头>();

            // Bonus pattern
            AddBonusPattern<赤宝牌>();
            AddBonusPattern<宝牌>();
            AddBonusPattern<里宝牌>();
        }
    }
}