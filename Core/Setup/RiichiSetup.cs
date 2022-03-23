using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Action.Resolver;
using RabiRiichi.Pattern;


namespace RabiRiichi.Core.Setup {
    public class RiichiSetup : BaseSetup {
        protected override void InjectResolvers(IServiceCollection collection) {
            collection.TryAddSingleton<ChanKanResolver>();
            collection.TryAddSingleton<ChiiResolver>();
            collection.TryAddSingleton<KanResolver>();
            collection.TryAddSingleton<PlayTileResolver>();
            collection.TryAddSingleton<PonResolver>();
            collection.TryAddSingleton<RiichiResolver>();
            collection.TryAddSingleton<RonResolver>();
            collection.TryAddSingleton<RyuukyokuResolver>();
            collection.TryAddSingleton<TenhouResolver>();
            collection.TryAddSingleton<TsumoResolver>();
        }

        protected override void InitPatterns() {
            // Base patterns
            AddBasePattern<Base33332>();
            AddBasePattern<Base72>();
            AddBasePattern<Base13_1>();

            // Std patterns
            AddStdPattern<Ippatsu>();
            AddStdPattern<Iipeikou>();
            AddStdPattern<RinshanKaihou>();
            AddStdPattern<Pinfu>();
            AddStdPattern<YakuhaiChun>();
            AddStdPattern<YakuhaiHatsu>();
            AddStdPattern<YakuhaiBakaze>();
            AddStdPattern<YakuhaiHaku>();
            AddStdPattern<YakuhaiJikaze>();
            AddStdPattern<Tanyao>();
            AddStdPattern<Chankan>();
            AddStdPattern<HouteiRaoyui>();
            AddStdPattern<HaiteiRaoyue>();
            AddStdPattern<Riichi>();
            AddStdPattern<MenzenchinTsumohou>();
            AddStdPattern<JunchanTaiyao>();
            AddStdPattern<Ittsu>();
            AddStdPattern<Sanankou>();
            AddStdPattern<Sankantsu>();
            AddStdPattern<SanshokuDoukou>();
            AddStdPattern<DoubleRiichi>();
            AddStdPattern<Toitoi>();
            AddStdPattern<Chantaiyao>();
            AddStdPattern<Honroutou>();
            AddStdPattern<Suuankou>();
            AddStdPattern<Suukantsu>();
            AddStdPattern<Chinroutou>();

            // Bonus pattern
            AddBonusPattern<Akadora>();
            AddBonusPattern<Dora>();
            AddBonusPattern<Uradora>();
        }
    }
}