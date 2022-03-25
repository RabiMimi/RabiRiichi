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

            // 1 han
            AddStdPattern<Chankan>();
            AddStdPattern<HaiteiRaoyue>();
            AddStdPattern<HouteiRaoyui>();
            AddStdPattern<Iipeikou>();
            AddStdPattern<Ippatsu>();
            AddStdPattern<MenzenchinTsumohou>();
            AddStdPattern<Pinfu>();
            AddStdPattern<Riichi>();
            AddStdPattern<RinshanKaihou>();
            AddStdPattern<Tanyao>();
            AddStdPattern<YakuhaiBakaze>();
            AddStdPattern<YakuhaiChun>();
            AddStdPattern<YakuhaiHaku>();
            AddStdPattern<YakuhaiHatsu>();
            AddStdPattern<YakuhaiJikaze>();

            // 2 han
            AddStdPattern<Chantaiyao>();
            AddStdPattern<DoubleRiichi>();
            AddStdPattern<Honroutou>();
            AddStdPattern<Ittsu>();
            AddStdPattern<Sanankou>();
            AddStdPattern<Sankantsu>();
            AddStdPattern<SanshokuDoujun>();
            AddStdPattern<SanshokuDoukou>();
            AddStdPattern<JunchanTaiyao>();
            AddStdPattern<Shousangen>();
            AddStdPattern<Toitoi>();

            // 3 han
            AddStdPattern<Honitsu>();
            AddStdPattern<JunchanTaiyao>();
            AddStdPattern<Ryanpeikou>();

            // 4 han+
            AddStdPattern<Chinitsu>();

            // Fu
            AddStdPattern<Fu13_1>();
            AddStdPattern<Fu72>();
            AddStdPattern<Fu33332>();

            // Yakuman
            AddStdPattern<Chiihou>();
            AddStdPattern<Chinroutou>();
            AddStdPattern<ChuurenPoutou>();
            AddStdPattern<Daisangen>();
            AddStdPattern<Daisuushii>();
            AddStdPattern<HelloWorld>();
            AddStdPattern<JunseiChuurenPoutou>();
            AddStdPattern<KokushiMusou>();
            AddStdPattern<KokushiMusouJuusanmenMachi>();
            AddStdPattern<Ryuuiisou>();
            AddStdPattern<Shousuushii>();
            AddStdPattern<Suuankou>();
            // TODO: Uncomment when ready
            // AddStdPattern<SuuankouTanki>();
            AddStdPattern<Suukantsu>();
            AddStdPattern<Tenhou>();
            AddStdPattern<Tsuuiisou>();

            // Bonus pattern
            AddBonusPattern<Akadora>();
            AddBonusPattern<Dora>();
            AddBonusPattern<Uradora>();
        }
    }
}