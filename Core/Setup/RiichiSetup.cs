using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Actions.Resolver;
using RabiRiichi.Patterns;


namespace RabiRiichi.Core.Setup {
  public class RiichiSetup : BaseSetup {
    protected override void InjectResolvers(IServiceCollection collection) {
      collection.TryAddSingleton<ChanAnkanResolver>();
      collection.TryAddSingleton<ChankanResolver>();
      collection.TryAddSingleton<ChiiResolver>();
      collection.TryAddSingleton<KanResolver>();
      collection.TryAddSingleton<NukiChankanResolver>();
      collection.TryAddSingleton<NukiDoraResolver>();
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
      AddBasePattern<BaseShiisanputa>();
      AddBasePattern<BaseShiisuuputa>();

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
      AddStdPattern<TsubameGaeshi>();
      AddStdPattern<Kanburi>();
      AddStdPattern<Ryanankan>();
      AddStdPattern<Chaopaikou>();
      AddStdPattern<Chinpaikou>();
      AddStdPattern<ShiiaruRaotai>();

      // 2 han
      AddStdPattern<Chiitoitsu>();
      AddStdPattern<Chantaiyao>();
      AddStdPattern<DoubleRiichi>();
      AddStdPattern<Honroutou>();
      AddStdPattern<Ittsu>();
      AddStdPattern<Sanankou>();
      AddStdPattern<Sankantsu>();
      AddStdPattern<SanshokuDoujun>();
      AddStdPattern<SanshokuDoukou>();
      AddStdPattern<Shousangen>();
      AddStdPattern<Toitoi>();
      AddStdPattern<Shousanfon>();
      AddStdPattern<Sanrenkou>();
      AddStdPattern<Sanfonkou>();
      AddStdPattern<Sanankan>();
      AddStdPattern<Uumensai>();

      // 3 han
      AddStdPattern<Honitsu>();
      AddStdPattern<JunchanTaiyao>();
      AddStdPattern<Ryanpeikou>();
      AddStdPattern<IsshokuSandoujun>();
      AddStdPattern<Chinpeikou>();

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
      AddStdPattern<SuuankouTanki>();
      AddStdPattern<Suukantsu>();
      AddStdPattern<Tenhou>();
      AddStdPattern<Tsuuiisou>();
      AddStdPattern<Suurenkou>();
      AddStdPattern<IsshokuYondoujun>();
      AddStdPattern<Renhou>();
      AddStdPattern<Daisharin>();
      AddStdPattern<Daichikurin>();
      AddStdPattern<Daisuurin>();
      AddStdPattern<Shiisanputa>();
      AddStdPattern<Shiisuuputa>();
      AddStdPattern<Paarenchan>();
      AddStdPattern<Benikujaku>();
      AddStdPattern<Heiiisou>();
      AddStdPattern<Daichiishin>();
      AddStdPattern<IshiNoUeNiMoSannen>();
      AddStdPattern<Katengecchi>();

      // Bonus pattern
      AddStdPattern<Akadora>();
      AddStdPattern<Dora>();
      AddStdPattern<NukiDora>();
      AddStdPattern<Uradora>();
    }
  }
}