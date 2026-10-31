using System;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;

namespace RabiRiichi.Arena.Config {
  /// <summary>
  /// Authoritative catalog of engine yaku (standard + koyaku), tile-set presets,
  /// policy bitmasks, LLM providers, and baseline variants exposed to the Arena
  /// prompt builder, config validator, and Web UI Configuration Studio.
  /// </summary>
  public static class ArenaMetadata {
    public sealed record YakuCatalogItem(
        string Name,
        string DisplayName,
        string Group,
        string HanSummary,
        string Description,
        bool DefaultEnabled = true);

    public sealed record PolicyFlagItem(
        int Value,
        string Key,
        string Label,
        string Description);

    public sealed record PolicyCategoryItem(
        string Key,
        string Label,
        int DefaultValue,
        bool IsEnum,
        IReadOnlyList<PolicyFlagItem> Options);

    public sealed record TilePresetItem(
        string Id,
        string Label,
        string Description,
        int BaseTileCount,
        int DefaultWildcards,
        int RecommendedPlayers);

    public sealed record ProviderPresetItem(
        string Id,
        string Label,
        string DefaultBaseUrl,
        string PlaceholderModel,
        bool RequiresApiKey,
        string Description);

    public sealed record BaselineVariantItem(
        string Id,
        string Label,
        double SuggestedFrozenElo,
        string Description);

    /// <summary>
    /// Canonical ordered list of all 38 distinct physical tile codes in RabiRiichi:
    /// 9 manzu + r5m, 9 pinzu + r5p, 9 souzu + r5s, 7 honors, and * wildcard.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalTileCodes = [
      "1m", "2m", "3m", "4m", "5m", "r5m", "6m", "7m", "8m", "9m",
      "1p", "2p", "3p", "4p", "5p", "r5p", "6p", "7p", "8p", "9p",
      "1s", "2s", "3s", "4s", "5s", "r5s", "6s", "7s", "8s", "9s",
      "1z", "2z", "3z", "4z", "5z", "6z", "7z",
      "*",
    ];

    public static readonly IReadOnlyList<TilePresetItem> TilePresets = [
      new("Regular", "Standard 4-Player (136)", "Classic 136-tile Riichi deck with 3 red fives (r5m, r5p, r5s).", 136, 0, 4),
      new("RegularWildcards", "Standard + 4 Wildcards (140)", "Standard 136-tile deck plus 4 universal wildcard (*) tiles.", 136, 4, 4),
      new("NoAkaRegular", "Classic No-Red (136)", "Traditional 136-tile deck with zero red dora tiles.", 136, 0, 4),
      new("Sanma", "Sanma 3-Player (108)", "Removes 2m-8m; keeps 1m/9m, all pinzu/souzu (r5p, r5s), and all honors.", 108, 0, 3),
      new("SanmaWildcards", "Sanma + 3 Wildcards (111)", "108-tile 3-player Sanma deck plus 3 universal wildcard (*) tiles.", 108, 3, 3),
      new("TwoSets", "Double Deck (272)", "Two complete 136-tile decks combined (up to 8 copies per tile).", 272, 0, 4),
      new("TwoSetsWildcards", "Double Deck + 8 Wildcards (280)", "Two complete 136-tile decks plus 8 universal wildcard (*) tiles.", 272, 8, 4),
      new("OnlySZ", "Bamboo & Honors Only (64)", "High-scoring flush stress test: only Souzu (1s-9s + r5s) and Honors (1z-7z).", 64, 0, 2),
      new("OnlySZWildcards", "Bamboo & Honors + 4 Wildcards (68)", "Souzu + Honors deck augmented with 4 universal wildcard (*) tiles.", 64, 4, 3),
      new("TenchiSouzou", "Tenchi Souzou (136x Haku)", "Extreme stress deck containing 136 White Dragon (5z) tiles.", 136, 0, 4),
      new("Custom", "Custom Tile Composition", "Specify exact per-tile counts across all 34 kinds, red fives, and wildcards.", 136, 0, 4),
    ];

    public static readonly IReadOnlyList<ProviderPresetItem> Providers = [
      new("openai", "OpenAI / Compatible", "https://api.openai.com", "gpt-5-mini", true,
          "OpenAI Chat Completions API and compatible endpoints (DeepSeek, OpenRouter, vLLM, Ollama, Qwen, Together)."),
      new("gemini", "Google Gemini", "https://generativelanguage.googleapis.com", "gemini-2.5-flash", true,
          "Google Gemini Interactions API (/v1beta/interactions) with native thinking_level support."),
      new("grok", "xAI Grok", "https://api.x.ai", "grok-4.3-fast", true,
          "xAI Grok API using max_completion_tokens and reasoning_effort."),
      new("anthropic", "Anthropic Claude", "https://api.anthropic.com", "claude-sonnet-4-20250514", true,
          "Anthropic Messages API (/v1/messages) with optional extended thinking."),
      new("baseline", "Rule-Based Baseline", "", "", false,
          "Deterministic/expected-value local Mahjong engine anchor (no network calls, zero cost)."),
    ];

    public static readonly IReadOnlyList<BaselineVariantItem> BaselineVariants = [
      new("balanced", "Balanced EV (Default)", 1500,
          "Full RuleBasedStrategy: prices hand value, min-han routes, and deal-in danger in points."),
      new("aggressive", "Aggressive Pusher", 1480,
          "Declares riichi and pushes tenpai/iishanten hands aggressively even into opponent threats."),
      new("defensive", "Cautious Folder (Betaori)", 1490,
          "Folds early to safest genbutsu/suji tiles whenever an opponent declares riichi or shows heavy melds."),
      new("fast_open", "Speed Caller (Fuuro)", 1450,
          "Calls chii/pon eagerly whenever an open yaku route exists to race quick cheap hands."),
      new("tsumogiri", "Passive Tsumogiri (Floor)", 1100,
          "Takes legal Ron/Tsumo wins when available; otherwise discards the drawn tile without calling."),
    ];

    public static readonly IReadOnlyList<YakuCatalogItem> Yakus = [
      // 1 Han
      new("Riichi", "Riichi", "1han", "1 han (closed)", "Declare tenpai on a closed hand for 1000 pts."),
      new("Ippatsu", "Ippatsu", "1han", "1 han (closed)", "Win within one uninterrupted turn cycle after declaring Riichi."),
      new("MenzenchinTsumohou", "Menzen Tsumo", "1han", "1 han (closed)", "Self-draw win on a fully concealed hand."),
      new("Tanyao", "Tanyao (All Simples)", "1han", "1 han", "Only numbered tiles 2-8 (no terminals or honors)."),
      new("Pinfu", "Pinfu", "1han", "1 han (closed)", "Four sequences, valueless pair, and two-sided (ryanmen) wait."),
      new("Iipeikou", "Iipeikou (Pure Double Chi)", "1han", "1 han (closed)", "Two identical sequences in the same suit."),
      new("YakuhaiBakaze", "Prevalent Wind", "1han", "1 han", "Triplet/quad of the current round wind."),
      new("YakuhaiJikaze", "Seat Wind", "1han", "1 han", "Triplet/quad of your own seat wind."),
      new("YakuhaiHaku", "White Dragon (Haku)", "1han", "1 han", "Triplet/quad of 5z (White Dragon)."),
      new("YakuhaiHatsu", "Green Dragon (Hatsu)", "1han", "1 han", "Triplet/quad of 6z (Green Dragon)."),
      new("YakuhaiChun", "Red Dragon (Chun)", "1han", "1 han", "Triplet/quad of 7z (Red Dragon)."),
      new("RinshanKaihou", "After a Kan (Rinshan)", "1han", "1 han", "Win on the dead-wall replacement draw immediately after a kan."),
      new("Chankan", "Robbing a Kan (Chankan)", "1han", "1 han", "Win off a tile another player adds to an open triplet (kakan)."),
      new("HaiteiRaoyue", "Under the Sea (Haitei)", "1han", "1 han", "Tsumo win on the final drawable tile of the live wall."),
      new("HouteiRaoyui", "Under the River (Houtei)", "1han", "1 han", "Ron win on the discard following the final live-wall draw."),

      // 2 Han
      new("DoubleRiichi", "Double Riichi", "2han", "2 han (closed)", "Declare Riichi on your very first uninterrupted turn."),
      new("SanshokuDoujun", "Mixed Triple Chi", "2han", "2 han (1 open)", "Same numbered sequence (e.g. 234) across all three suits."),
      new("Ittsu", "Pure Straight (Ittsu)", "2han", "2 han (1 open)", "123, 456, and 789 in a single suit."),
      new("Chantaiyao", "Half Outside Hand (Chanta)", "2han", "2 han (1 open)", "Every group and pair contains a terminal or honor, with at least one sequence."),
      new("Toitoi", "All Pongs (Toitoi)", "2han", "2 han", "Four triplets/quads and a pair."),
      new("Sanankou", "Three Concealed Pongs", "2han", "2 han", "Three triplets/quads formed without calling them."),
      new("SanshokuDoukou", "Triple Pong", "2han", "2 han", "Triplets/quads of the same number in all three suits."),
      new("Sankantsu", "Three Kans", "2han", "2 han", "Three quads declared in one hand."),
      new("Honroutou", "All Terminals & Honors", "2han", "2 han", "Every tile is 1, 9, or an honor (combines with Toitoi or Chiitoitsu)."),
      new("Shousangen", "Little Three Dragons", "2han", "2 han", "Two dragon triplets/quads plus a pair of the third dragon."),
      new("Chiitoitsu", "Seven Pairs", "2han", "2 han (closed)", "Seven distinct pairs (25 fu)."),

      // 3 Han
      new("Honitsu", "Half Flush (Honitsu)", "3han", "3 han (2 open)", "Tiles from only one numbered suit plus honor tiles."),
      new("JunchanTaiyao", "Pure Outside Hand (Junchan)", "3han", "3 han (2 open)", "Every group and pair contains 1 or 9, with no honor tiles."),
      new("Ryanpeikou", "Twice Pure Double Chi", "3han", "3 han (closed)", "Two separate pairs of identical sequences."),

      // 6 Han
      new("Chinitsu", "Full Flush (Chinitsu)", "6han", "6 han (5 open)", "All tiles belong to a single numbered suit (no honors)."),

      // Yakuman
      new("Tenhou", "Blessing of Heaven (Tenhou)", "yakuman", "Yakuman", "Dealer wins on the initial 14-tile deal."),
      new("Chiihou", "Blessing of Earth (Chiihou)", "yakuman", "Yakuman", "Non-dealer wins on their first uninterrupted self-draw."),
      new("Daisangen", "Big Three Dragons", "yakuman", "Yakuman", "Triplets/quads of all three dragons (5z, 6z, 7z)."),
      new("Suuankou", "Four Concealed Pongs", "yakuman", "Yakuman (closed)", "Four concealed triplets/quads won by tsumo."),
      new("SuuankouTanki", "Four Concealed Pongs Single Wait", "yakuman", "Double Yakuman", "Four concealed triplets/quads waiting on the pair tile."),
      new("Tsuuiisou", "All Honors", "yakuman", "Yakuman", "Every tile in the hand is an honor (1z-7z)."),
      new("Ryuuiisou", "All Green", "yakuman", "Yakuman", "Composed exclusively of 2s, 3s, 4s, 6s, 8s, and/or 6z."),
      new("Chinroutou", "All Terminals", "yakuman", "Yakuman", "Composed exclusively of 1 and 9 numbered tiles."),
      new("KokushiMusou", "Thirteen Orphans", "yakuman", "Yakuman (closed)", "One of each of the 13 terminal/honor kinds plus one duplicate."),
      new("KokushiMusouJuusanmenMachi", "Thirteen Orphans 13-Way Wait", "yakuman", "Double Yakuman", "All 13 terminal/honor kinds held before winning on any of the 13."),
      new("Shousuushii", "Little Four Winds", "yakuman", "Yakuman", "Three wind triplets/quads plus a pair of the fourth wind."),
      new("Daisuushii", "Big Four Winds", "yakuman", "Double Yakuman", "Triplets/quads of all four winds (1z-4z)."),
      new("Suukantsu", "Four Kans", "yakuman", "Yakuman", "Four quads declared in a single hand."),
      new("ChuurenPoutou", "Nine Gates", "yakuman", "Yakuman (closed)", "1112345678999 in one suit plus any extra tile of that suit."),
      new("JunseiChuurenPoutou", "Pure Nine Gates", "yakuman", "Double Yakuman", "1112345678999 concealed hand winning on the 9-way wait."),

      // Classical Yaku (Koyaku — default off)
      new("TsubameGaeshi", "Tsubame Gaeshi (Swallow Return)", "koyaku", "1 han", "Ron win directly on another player's Riichi declaration tile.", false),
      new("Kanburi", "Kanburi", "koyaku", "1 han", "Ron win on the discard immediately following a kan replacement draw.", false),
      new("ShiiaruRaotai", "Shiiaru Raotai (Twelve Exposed)", "koyaku", "1 han", "Four open called melds leaving a single pair wait.", false),
      new("Shousanfon", "Shousanfon (Little Three Winds)", "koyaku", "1 han", "Two wind triplets/quads plus a pair of a third wind.", false),
      new("Sanrenkou", "Sanrenkou (Three Consecutive Pongs)", "koyaku", "2 han", "Three consecutive numbered triplets in the same suit (e.g. 333m 444m 555m).", false),
      new("Sanfonkou", "Sanfonkou (Three Wind Pongs)", "koyaku", "2 han", "Triplets/quads of three different winds.", false),
      new("Chaopaikou", "Chaopaikou (Three Stepped Pongs)", "koyaku", "2 han", "Three triplets shifted by 2 in one suit (1-3-5, 2-4-6, 3-5-7, 4-6-8, or 5-7-9).", false),
      new("Chinpaikou", "Chinpaikou (Three Suji Pongs)", "koyaku", "2 han", "Three suji-spaced triplets in one suit (1-4-7, 2-5-8, or 3-6-9).", false),
      new("Uumensai", "Uumensai (All Five Types)", "koyaku", "2 han", "Hand contains Manzu, Pinzu, Souzu, Winds, and Dragons across its 5 groups.", false),
      new("Ryanankan", "Ryanankan (Two Concealed Kans)", "koyaku", "2 han", "Two concealed quads (ankan) in one hand.", false),
      new("IsshokuSandoujun", "Isshoku Sandoujun (Triple Identical Chi)", "koyaku", "3 han (2 open)", "Three identical sequences in the same suit.", false),
      new("Chinpeikou", "Chinpeikou (Two-Suit Ryanpeikou)", "koyaku", "3 han (closed)", "Ryanpeikou where both sequence pairs share the same numbers across suits or one suit.", false),
      new("Suurenkou", "Suurenkou (Four Consecutive Pongs)", "koyaku", "Yakuman", "Four consecutive numbered triplets in one suit.", false),
      new("IsshokuYondoujun", "Isshoku Yondoujun (Quadruple Identical Chi)", "koyaku", "Yakuman", "Four identical sequences in one suit.", false),
      new("Sanankan", "Sanankan (Three Concealed Kans)", "koyaku", "Yakuman", "Three concealed quads (ankan) in one hand.", false),
      new("Renhou", "Renhou (Blessing of Man)", "koyaku", "Yakuman", "Non-dealer Ron win before their first draw with no prior calls.", false),
      new("Katengecchi", "Katengecchi (Blossom & Moon)", "koyaku", "Yakuman", "Rinshan Kaihou self-draw win specifically on 5p or 1s.", false),
      new("IshiNoUeNiMoSannen", "Ishi no Ue ni mo Sannen", "koyaku", "Yakuman", "Double Riichi combined with Haitei or Houtei win.", false),
      new("Heiiisou", "Heiiisou (All Black)", "koyaku", "Yakuman", "Composed exclusively of 2p, 4p, 8p, and Wind honors (1z-4z).", false),
      new("Benikujaku", "Benikujaku (Red Peacock)", "koyaku", "Yakuman", "Composed exclusively of 1s, 5s, 7s, 9s, and Red Dragon (7z).", false),
      new("Daisharin", "Daisharin (Big Wheels)", "koyaku", "Yakuman (closed)", "Seven pairs of 2p, 3p, 4p, 5p, 6p, 7p, 8p.", false),
      new("Daichikurin", "Daichikurin (Big Bamboo Forest)", "koyaku", "Yakuman (closed)", "Seven pairs of 2s, 3s, 4s, 5s, 6s, 7s, 8s.", false),
      new("Daisuurin", "Daisuurin (Big Numbers)", "koyaku", "Yakuman (closed)", "Seven pairs of 2m, 3m, 4m, 5m, 6m, 7m, 8m.", false),
      new("Shiisanputa", "Shiisanputa (13 Unconnected)", "koyaku", "Yakuman (closed)", "First-turn win where 13 tiles have no adjacent/2-away suit neighbors and 1 pair.", false),
      new("Shiisuuputa", "Shiisuuputa (14 Unconnected)", "koyaku", "Yakuman (closed)", "First-turn win where all 14 tiles are distinct and non-related (at least 3 apart in suits).", false),
      new("Paarenchan", "Paarenchan (Eight Consecutive Wins)", "koyaku", "Bonus Yakuman", "Win on the 8th+ honba streak.", false),
      new("Daichiishin", "Daichiishin (Big Seven Stars)", "koyaku", "Double Yakuman", "Seven distinct honor pairs (1z, 2z, 3z, 4z, 5z, 6z, 7z).", false),

      // Other
      new("HelloWorld", "HelloWorld", "other", "Yakuman", "Special easter-egg pattern enabled in standard RabiRiichi setup.", true),
    ];

    public static readonly IReadOnlyDictionary<string, YakuCatalogItem> YakuByName =
        Yakus.ToDictionary(y => y.Name, StringComparer.Ordinal);

    public static IReadOnlyList<string> DefaultAllowedYakus() =>
        Yakus.Where(y => y.DefaultEnabled).Select(y => y.Name).ToList();

    public static readonly IReadOnlyList<PolicyCategoryItem> PolicyCategories = [
      new("renchanPolicy", "Dealer Repeat (Renchan) Policy", (int)RenchanPolicy.Default, false, [
        new((int)RenchanPolicy.DealerWin, "dealerWin", "Dealer Agari Repeats", "Dealer keeps dealership when winning the hand."),
        new((int)RenchanPolicy.DealerTenpai, "dealerTenpai", "Dealer Tenpai Repeats", "Dealer keeps dealership when tenpai at exhaustive draw."),
        new((int)RenchanPolicy.EndGameRyuukyoku, "endGameRyuukyoku", "Always Repeat on Exhaustive Draw", "Force repeat on any exhaustive draw even if dealer is noten."),
        new((int)RenchanPolicy.MidGameRyuukyoku, "midGameRyuukyoku", "Abortive Draw Repeats", "Keep dealership and add honba on mid-game abortive draws."),
      ]),
      new("endGamePolicy", "Match End Policy", (int)EndGamePolicy.Default, false, [
        new((int)EndGamePolicy.PointsOutOfRange, "pointsOutOfRange", "Bust / Cap at Round End", "End match after settlement if any player falls below lower limit or exceeds upper cap."),
        new((int)EndGamePolicy.InstantPointsOutOfRange, "instantPointsOutOfRange", "Instant Bust (Tobi)", "End match immediately when a player busts."),
        new((int)EndGamePolicy.DealerTenpai, "dealerTenpai", "Agari-Yame / Tenpai-Yame", "In All-Last, end match if dealer is #1 at target points and tenpai on draw."),
        new((int)EndGamePolicy.DealerAgari, "dealerAgari", "Agari-Yame on Win", "In All-Last, end match if dealer wins and is #1 at or above target points."),
        new((int)EndGamePolicy.ExtendedRound, "extendedRound", "Sudden Death Extension (West/North)", "Extend into next wind round if nobody has reached target points."),
      ]),
      new("kuikaePolicy", "Swap-Calling (Kuikae) Ban", (int)KuikaePolicy.Default, false, [
        new((int)KuikaePolicy.Genbutsu, "genbutsu", "Forbid Exact-Tile Swap", "Cannot immediately discard the same tile kind just claimed in a chii/pon."),
        new((int)KuikaePolicy.Suji, "suji", "Forbid Suji Swap", "Cannot immediately discard the opposite-end suji tile that would have completed the chii."),
      ]),
      new("riichiPolicy", "Riichi Requirements", (int)RiichiPolicy.Default, false, [
        new((int)RiichiPolicy.SufficientPoints, "sufficientPoints", "Require Riichi Deposit", "Cannot declare Riichi if paying the deposit would bust you."),
        new((int)RiichiPolicy.ValidPoints, "validPoints", "Require Non-Negative Score", "Cannot declare Riichi while below the valid lower score bound."),
        new((int)RiichiPolicy.SufficientTiles, "sufficientTiles", "Require Full Draw Cycle Left", "Cannot declare Riichi when fewer than PlayerCount tiles remain in live wall."),
      ]),
      new("doraOption", "Dora & Nukidora Rules", (int)DoraOption.Default, false, [
        new((int)DoraOption.InitialDora, "initialDora", "Initial Dora", "Reveal 1 front dora indicator at the start of every round."),
        new((int)DoraOption.InitialUradora, "initialUradora", "Ura Dora", "Riichi winners score hidden ura-dora indicators beneath revealed dora."),
        new((int)DoraOption.KanDora, "kanDora", "Kan Dora", "Reveal an additional dora indicator for each kan declared."),
        new((int)DoraOption.KanUradora, "kanUradora", "Kan Ura Dora", "Include kan ura-dora indicators for Riichi winners."),
        new((int)DoraOption.InstantRevealAfterAnkan, "instantAnkan", "Instant Dora on Closed Kan", "Flip kan dora immediately on concealed kan (ankan)."),
        new((int)DoraOption.InstantRevealAfterDaiminkan, "instantDaiminkan", "Instant Dora on Open Kan", "Flip kan dora immediately on open daiminkan (instead of after discard)."),
        new((int)DoraOption.InstantRevealAfterKakan, "instantKakan", "Instant Dora on Added Kan", "Flip kan dora immediately on added kan (kakan)."),
        new((int)DoraOption.Nukidora, "nukidora", "Nukidora (Pull North 4z)", "Allow setting 4z aside for +1 bonus dora han + replacement draw (standard in 3P Sanma)."),
      ]),
      new("agariOption", "Winning (Agari) Options", (int)AgariOption.Default, false, [
        new((int)AgariOption.Kuitan, "kuitan", "Open Tanyao (Kuitan)", "Allow All Simples (Tanyao) on open melded hands."),
        new((int)AgariOption.Pao, "pao", "Pao (Liability Payment)", "Apply liability payment when feeding final dragon/wind triplet for Daisangen/Daisuushii."),
        new((int)AgariOption.NagashiMangan, "nagashiMangan", "Nagashi Mangan", "Award Mangan at exhaustive draw if all discards were terminals/honors and none were called."),
        new((int)AgariOption.FirstWinner, "firstWinner", "Head Bump (Atamahane)", "Only the closest player in turn order wins on multi-ron (off = double/triple ron allowed)."),
      ]),
      new("scoringOption", "Scoring & Limit Options", (int)ScoringOption.All, false, [
        new((int)ScoringOption.KiriageMangan, "kiriageMangan", "Kiriage Mangan", "Round 4 han 30 fu and 3 han 60 fu up to Mangan."),
        new((int)ScoringOption.Yakuman, "yakuman", "Standard Yakuman Cap", "Use standard limit caps (turn off for uncapped Aotenjou exponential scoring!)."),
        new((int)ScoringOption.MultipleYakuman, "multipleYakuman", "Multiple / Composite Yakuman", "Allow stacking multiple yakuman patterns in a single hand."),
        new((int)ScoringOption.KazoeYakuman, "kazoeYakuman", "Counted (Kazoe) Yakuman", "Hands reaching 13+ han score as a Yakuman."),
      ]),
      new("ryuukyokuTrigger", "Mid-Game Abortive Draws", (int)RyuukyokuTrigger.Default, false, [
        new((int)RyuukyokuTrigger.KyuushuKyuuhai, "kyuushuKyuuhai", "Nine Terminals/Honors (Kyuushu)", "Optional abortive redeal on first uninterrupted turn with 9+ terminal/honor kinds."),
        new((int)RyuukyokuTrigger.SuufonRenda, "suufonRenda", "Four Same Wind Discards", "Abortive draw if all 4 players discard the same wind tile on turn 1."),
        new((int)RyuukyokuTrigger.SuuchaRiichi, "suuchaRiichi", "Four Players Riichi", "Abortive draw if all 4 players declare Riichi."),
        new((int)RyuukyokuTrigger.Sanchahou, "sanchahou", "Triple Ron Abort", "Abortive draw when 3 players simultaneously declare Ron on one discard."),
        new((int)RyuukyokuTrigger.SuukanSanra, "suukanSanra", "Four Kans Abort", "Abortive draw when 4 kans are declared by two or more players."),
      ]),
      new("pointsDeductionPolicy", "Bust / Score Floor Policy", (int)PointsDeductionPolicy.SufficientPoints, true, [
        new((int)PointsDeductionPolicy.AlwaysAllow, "alwaysAllow", "Always Allow Negative Scores", "Scores may drop freely below 0 without restriction."),
        new((int)PointsDeductionPolicy.SufficientPoints, "sufficientPoints", "Standard Bust Floor (Default)", "Prevent voluntary point deductions that would push a player below lower bound."),
        new((int)PointsDeductionPolicy.ValidPoints, "validPoints", "Require Valid Score Before Deduction", "Allow deduction as long as current score is within valid bounds."),
        new((int)PointsDeductionPolicy.AlwaysBlock, "alwaysBlock", "Block Voluntary Deductions", "Disallow voluntary point deductions."),
      ]),
    ];

    /// <summary>
    /// Returns canonical per-tile counts for any built-in preset (before ExtraWildcards).
    /// </summary>
    public static Dictionary<string, int> GetPresetTileCounts(string preset) {
      var counts = CanonicalTileCodes.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);

      void FillRegular(int sets, bool withAka) {
        foreach (char s in new[] { 'm', 'p', 's' }) {
          for (int n = 1; n <= 9; n++) {
            if (n == 5 && withAka) {
              counts[$"5{s}"] += 3 * sets;
              counts[$"r5{s}"] += 1 * sets;
            } else {
              counts[$"{n}{s}"] += 4 * sets;
            }
          }
        }
        for (int z = 1; z <= 7; z++) {
          counts[$"{z}z"] += 4 * sets;
        }
      }

      switch ((preset ?? "Regular").Trim()) {
        case "RegularWildcards":
          FillRegular(1, withAka: true);
          counts["*"] = 4;
          break;
        case "NoAkaRegular":
          FillRegular(1, withAka: false);
          break;
        case "Sanma":
        case "SanmaWildcards":
          counts["1m"] = 4;
          counts["9m"] = 4;
          foreach (char s in new[] { 'p', 's' }) {
            for (int n = 1; n <= 9; n++) {
              if (n == 5) {
                counts[$"5{s}"] = 3;
                counts[$"r5{s}"] = 1;
              } else {
                counts[$"{n}{s}"] = 4;
              }
            }
          }
          for (int z = 1; z <= 7; z++) counts[$"{z}z"] = 4;
          if (string.Equals(preset?.Trim(), "SanmaWildcards", StringComparison.OrdinalIgnoreCase)) {
            counts["*"] = 3;
          }
          break;
        case "TwoSets":
          FillRegular(2, withAka: true);
          break;
        case "TwoSetsWildcards":
          FillRegular(2, withAka: true);
          counts["*"] = 8;
          break;
        case "OnlySZ":
        case "OnlySZWildcards":
          for (int n = 1; n <= 9; n++) {
            if (n == 5) {
              counts["5s"] = 3;
              counts["r5s"] = 1;
            } else {
              counts[$"{n}s"] = 4;
            }
          }
          for (int z = 1; z <= 7; z++) counts[$"{z}z"] = 4;
          if (string.Equals(preset?.Trim(), "OnlySZWildcards", StringComparison.OrdinalIgnoreCase)) {
            counts["*"] = 4;
          }
          break;
        case "TenchiSouzou":
          counts["5z"] = 136;
          break;
        default:
          FillRegular(1, withAka: true);
          break;
      }
      return counts;
    }

    /// <summary>
    /// Materializes a <see cref="Tiles"/> deck from a preset, optional extra wildcards,
    /// or an explicit custom tile-count map.
    /// </summary>
    public static Tiles BuildTiles(
        string preset,
        int extraWildcards = 0,
        IReadOnlyDictionary<string, int> customCounts = null) {
      Dictionary<string, int> counts;
      if (string.Equals(preset?.Trim(), "Custom", StringComparison.OrdinalIgnoreCase) &&
          customCounts != null && customCounts.Count > 0) {
        counts = CanonicalTileCodes.ToDictionary(c => c, _ => 0, StringComparer.Ordinal);
        foreach (var kv in customCounts) {
          string key = NormalizeTileCode(kv.Key);
          if (key != null && kv.Value > 0) {
            counts[key] = counts.GetValueOrDefault(key) + kv.Value;
          }
        }
      } else {
        counts = GetPresetTileCounts(preset);
      }

      if (extraWildcards > 0) {
        counts["*"] = counts.GetValueOrDefault("*") + extraWildcards;
      }

      var result = new Tiles();
      foreach (var code in CanonicalTileCodes) {
        if (!counts.TryGetValue(code, out int c) || c <= 0) continue;
        Tile tile = ParseCanonicalCode(code);
        for (int i = 0; i < c; i++) {
          result.Add(tile);
        }
      }
      return result;
    }

    public static string NormalizeTileCode(string raw) {
      if (string.IsNullOrWhiteSpace(raw)) return null;
      string t = raw.Trim();
      if (t == "*") return "*";
      if (t is "0m" or "r5m") return "r5m";
      if (t is "0p" or "r5p") return "r5p";
      if (t is "0s" or "r5s") return "r5s";
      return CanonicalTileCodes.Contains(t) ? t : null;
    }

    public static Tile ParseCanonicalCode(string code) => code switch {
      "*" => Tile.Wildcard,
      "r5m" => new Tile(TileSuit.M, 5, isAkadora: true),
      "r5p" => new Tile(TileSuit.P, 5, isAkadora: true),
      "r5s" => new Tile(TileSuit.S, 5, isAkadora: true),
      _ => new Tile(code),
    };
  }
}
