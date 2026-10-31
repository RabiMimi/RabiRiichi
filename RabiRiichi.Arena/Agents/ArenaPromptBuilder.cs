using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RabiRiichi.Arena.Config;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Core;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Agents.Llm;
using TileSource = RabiRiichi.Generated.Core.TileSource;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Builds system instructions, rich per-turn decision prompts, and compact
  /// condensed history summaries for Arena evaluation agents across English (<c>en</c>),
  /// Simplified Chinese (<c>zhs</c>), Traditional Chinese (<c>zht</c>), and Japanese (<c>ja</c>).
  ///
  /// Supports 2-4 player games, Sanma + nukidora, double/custom decks, universal
  /// wildcard (<c>*</c>) tiles, Aotenjou uncapped scoring, and Classical Yaku (<c>koyaku</c>).
  /// Strictly enforces the fair-play public-information boundary via <see cref="PublicGameView"/>
  /// and opponent anonymity via <see cref="ArenaSeatLabeler"/>.
  /// </summary>
  public sealed class ArenaPromptBuilder {
    private static readonly Tile[] AllStandardKinds = BuildStandardKinds();

    private static readonly IReadOnlyDictionary<string, (string Zhs, string Zht, string Ja)> KoyakuPromptText =
        new Dictionary<string, (string Zhs, string Zht, string Ja)>(StringComparer.Ordinal) {
          ["TsubameGaeshi"] = (
              "燕返 [1番]: 直接荣和他家的立直宣言牌。",
              "燕返 [1飜]: 直接榮和他家的立直宣言牌。",
              "燕返し [1飜]: 他家のリーチ宣言牌で直接ロン和了する。"),
          ["Kanburi"] = (
              "杠振 [1番]: 荣和他家岭上摸牌后紧接着打出的舍牌。",
              "槓振 [1飜]: 榮和他家嶺上摸牌後緊接著打出的捨牌。",
              "槓振り [1飜]: 槓の嶺上ツモ直後の打牌をロン和了する。"),
          ["ShiiaruRaotai"] = (
              "十二落抬 [1番]: 四副露明刻/顺后单骑听牌和牌。",
              "十二落抬 [1飜]: 四副露明刻/順後單騎聽牌和牌。",
              "十二落抬 [1飜]: 4副露して裸単騎待ちで和了する。"),
          ["Shousanfon"] = (
              "小三风 [1番]: 两组风牌刻子/杠子加第三种风牌雀头。",
              "小三風 [1飜]: 兩組風牌刻子/槓子加第三種風牌雀頭。",
              "小三風 [1飜]: 風牌2種の刻子・槓子と3種目の風牌の雀頭。"),
          ["Sanrenkou"] = (
              "三连刻 [2番]: 同花色连续三个数字的刻子/杠子。",
              "三連刻 [2飜]: 同花色連續三個數字的刻子/槓子。",
              "三連刻 [2飜]: 同種数牌で連続する3つの数字の刻子・槓子。"),
          ["Sanfonkou"] = (
              "三风刻 [2番]: 三种不同风牌的刻子/杠子。",
              "三風刻 [2飜]: 三種不同風牌的刻子/槓子。",
              "三風刻 [2飜]: 3種類の風牌の刻子・槓子。"),
          ["Chaopaikou"] = (
              "跳牌刻 [2番]: 同花色间隔2的三组刻子（如1-3-5、2-4-6等）。",
              "跳牌刻 [2飜]: 同花色間隔2的三組刻子（如1-3-5、2-4-6等）。",
              "跳牌刻 [2飜]: 同種数牌で1つ飛ばしの数字3組の刻子（1-3-5、2-4-6など）。"),
          ["Chinpaikou"] = (
              "筋牌刻 [2番]: 同花色筋牌间隔的三组刻子（1-4-7、2-5-8或3-6-9）。",
              "筋牌刻 [2飜]: 同花色筋牌間隔的三組刻子（1-4-7、2-5-8或3-6-9）。",
              "筋牌刻 [2飜]: 同種数牌でスジ関係3組の刻子（1-4-7、2-5-8、3-6-9）。"),
          ["Uumensai"] = (
              "五门齐 [2番]: 手牌五组面子/雀头同时包含万、筒、索、风牌与三元牌。",
              "五門齊 [2飜]: 手牌五組面子/雀頭同時包含萬、筒、索、風牌與三元牌。",
              "五門斉 [2飜]: 萬子・筒子・索子・風牌・三元牌の5種類すべてを含む和了形。"),
          ["Ryanankan"] = (
              "二暗杠 [2番]: 手牌包含两组暗杠。",
              "二暗槓 [2飜]: 手牌包含兩組暗槓。",
              "二暗槓 [2飜]: 1回の和了に暗槓が2つ含まれる。"),
          ["IsshokuSandoujun"] = (
              "一色三同顺 [3番（副露2番）]: 同花色三组完全相同的顺子。",
              "一色三同順 [3飜（副露2飜）]: 同花色三組完全相同的順子。",
              "一色三同順 [3飜（喰い下がり2飜）]: 同種数牌で同じ順子を3組揃える。"),
          ["Chinpeikou"] = (
              "清杯口 [3番（门清限定）]: 两对相同顺子具有相同数字。",
              "清盃口 [3飜（門清限定）]: 兩對相同順子具有相同數字。",
              "清盃口 [3飜（門前限定）]: 同じ数字構成の順子ペア2組による二盃口。"),
          ["Suurenkou"] = (
              "四连刻 [役满]: 同花色连续四个数字的刻子/杠子。",
              "四連刻 [役滿]: 同花色連續四個數字的刻子/槓子。",
              "四連刻 [役満]: 同種数牌で連続する4つの数字の刻子・槓子。"),
          ["IsshokuYondoujun"] = (
              "一色四同顺 [役满]: 同花色四组完全相同的顺子。",
              "一色四同順 [役滿]: 同花色四組完全相同的順子。",
              "一色四同順 [役満]: 同種数牌で同じ順子を4組揃える。"),
          ["Sanankan"] = (
              "三暗杠 [役满]: 手牌包含三组暗杠。",
              "三暗槓 [役滿]: 手牌包含三組暗槓。",
              "三暗槓 [役満]: 1回の和了に暗槓が3つ含まれる。"),
          ["Renhou"] = (
              "人和 [役满]: 闲家在无人鸣牌的第一巡摸牌前荣和。",
              "人和 [役滿]: 閒家在無人鳴牌的第一巡摸牌前榮和。",
              "人和 [役満]: 子が鳴きのない第一ツモ前にロン和了する。"),
          ["Katengecchi"] = (
              "花天月地 [役满]: 以 5p 或 1s 岭上开花自摸和牌。",
              "花天月地 [役滿]: 以 5p 或 1s 嶺上開花自摸和牌。",
              "花天月地 [役満]: 5p または 1s で嶺上開花ツモ和了する。"),
          ["IshiNoUeNiMoSannen"] = (
              "石上三年 [役满]: 双立直且海底摸月或河底捞鱼和牌。",
              "石上三年 [役滿]: 雙立直且海底撈月或河底撈魚和牌。",
              "石の上にも三年 [役満]: ダブル立直かつ海底摸月または河底撈魚で和了する。"),
          ["Heiiisou"] = (
              "黑一色 [役满]: 仅由 2p、4p、8p 与风牌（1z-4z）组成。",
              "黑一色 [役滿]: 僅由 2p、4p、8p 與風牌（1z-4z）組成。",
              "黒一色 [役満]: 2p・4p・8p・風牌（1z-4z）のみで構成された手。"),
          ["Benikujaku"] = (
              "红孔雀 [役满]: 仅由 1s、5s、7s、9s 与红中（7z）组成。",
              "紅孔雀 [役滿]: 僅由 1s、5s、7s、9s 與紅中（7z）組成。",
              "紅孔雀 [役満]: 1s・5s・7s・9s・中（7z）のみで構成された手。"),
          ["Daisharin"] = (
              "大车轮 [役满（门清限定）]: 筒子 2p-8p 的七对子。",
              "大車輪 [役滿（門清限定）]: 筒子 2p-8p 的七對子。",
              "大車輪 [役満（門前限定）]: 2p〜8p の連続7対子。"),
          ["Daichikurin"] = (
              "大竹林 [役满（门清限定）]: 索子 2s-8s 的七对子。",
              "大竹林 [役滿（門清限定）]: 索子 2s-8s 的七對子。",
              "大竹林 [役満（門前限定）]: 2s〜8s の連続7対子。"),
          ["Daisuurin"] = (
              "大数邻 [役满（门清限定）]: 万子 2m-8m 的七对子。",
              "大數鄰 [役滿（門清限定）]: 萬子 2m-8m 的七對子。",
              "大数隣 [役満（門前限定）]: 2m〜8m の連続7対子。"),
          ["Shiisanputa"] = (
              "十三不塔 [役满（门清限定）]: 第一巡和牌，13张互不靠张且含1对雀头。",
              "十三不塔 [役滿（門清限定）]: 第一巡和牌，13張互不靠張且含1對雀頭。",
              "十三不塔 [役満（門前限定）]: 第一巡和了で雀頭1組＋残り12枚がすべて不塔（筋・隣接なし）。"),
          ["Shiisuuputa"] = (
              "十四不塔 [役满（门清限定）]: 第一巡和牌，14张牌彼此均不相邻/隔张且无对子。",
              "十四不塔 [役滿（門清限定）]: 第一巡和牌，14張牌彼此均不相鄰/隔張且無對子。",
              "十四不塔 [役満（門前限定）]: 第一巡和了で14枚すべてが対子・塔子を持たない形。"),
          ["Paarenchan"] = (
              "八连庄 [役满]: 八本场及以上连庄和牌。",
              "八連莊 [役滿]: 八本場及以上連莊和牌。",
              "八連荘 [役満]: 8本場以上の連荘中に和了する。"),
          ["Daichiishin"] = (
              "大七星 [双倍役满]: 字牌 1z-7z 各一对组成的七对子。",
              "大七星 [雙倍役滿]: 字牌 1z-7z 各一對組成的七對子。",
              "大七星 [ダブル役満]: 字牌7種（1z〜7z）すべての対子による七対子。"),
        };

    private readonly ArenaSeatLabeler labeler;
    private readonly ArenaConfig.PromptConfig promptConfig;
    private readonly string language;

    public ArenaPromptBuilder(
        ArenaSeatLabeler labeler,
        ArenaConfig.PromptConfig promptConfig = null,
        string language = null) {
      this.labeler = labeler ?? throw new ArgumentNullException(nameof(labeler));
      this.promptConfig = promptConfig ?? new ArenaConfig.PromptConfig();
      this.language = NormalizeArenaLanguage(language ?? this.promptConfig.Language);
    }

    public string Language => language;

    /// <summary>
    /// Normalizes a locale tag into one of the four supported Arena locales:
    /// <c>en</c>, <c>zhs</c>, <c>zht</c>, or <c>ja</c>.
    /// </summary>
    public static string NormalizeArenaLanguage(string raw) {
      if (string.IsNullOrWhiteSpace(raw)) return "en";
      var tag = raw.Trim().ToLowerInvariant();
      if (tag is "zht" or "zh-tw" or "zh-hk" or "zh-mo" || tag.Contains("hant")) {
        return "zht";
      }
      if (tag is "zhs" or "zh" or "zh-cn" or "zh-sg" || tag.StartsWith("zh", StringComparison.Ordinal)) {
        return "zhs";
      }
      if (tag == "ja" || tag.StartsWith("ja-", StringComparison.Ordinal) || tag.StartsWith("ja_", StringComparison.Ordinal)) {
        return "ja";
      }
      return "en";
    }

    public string BuildSystemPrompt(int selfSeat, PublicGameView view) {
      string appendixHeading = language switch {
        "zhs" => "附加对局说明",
        "zht" => "附加對局說明",
        "ja" => "追加対局指示",
        _ => "Additional Match Instructions",
      };
      var customAppendix = string.IsNullOrWhiteSpace(promptConfig.CustomSystemAppendix)
          ? ""
          : $"\n## {appendixHeading}\n{promptConfig.CustomSystemAppendix.Trim()}\n";

      return LoadTemplate("arena-system", language)
          .Replace("{{SELF_SEAT}}", selfSeat.ToString())
          .Replace("{{PLAYER_COUNT}}", view.PlayerCount.ToString())
          .Replace("{{TILE_NOTATION}}", TileNotationHint(language))
          .Replace("{{DECK_RULES}}", BuildDeckRules(view))
          .Replace("{{GAME_RULES}}", BuildGameRules(view))
          .Replace("{{CUSTOM_APPENDIX}}", customAppendix)
          .Replace("{{OUTPUT_CONTRACT}}", BuildOutputContract())
          .TrimEnd();
    }

    public string BuildDeckRules(PublicGameView view) {
      var tiles = view.InitialTiles;
      if (tiles == null || tiles.Count == 0) {
        return language switch {
          "zhs" => "- 标准 136 张日本立直麻将牌组（每种数牌/字牌各 4 张，3 张红宝牌，0 张万能牌）。",
          "zht" => "- 標準 136 張日本立直麻將牌組（每種數牌/字牌各 4 張，3 張紅寶牌，0 張萬能牌）。",
          "ja" => "- 標準136枚の日本リーチ麻雀牌セット（各数牌・字牌4枚、赤五3枚、オールマイティ0枚）。",
          _ => "- Standard 136-tile Japanese Riichi deck (4 copies of each suited/honor tile, 3 red fives, 0 wildcards).",
        };
      }

      var sb = new StringBuilder();
      int total = tiles.Count;
      int wildcards = tiles.Count(t => t.IsWildcard);
      int redM = tiles.Count(t => t.Suit == TileSuit.M && t.Akadora);
      int redP = tiles.Count(t => t.Suit == TileSuit.P && t.Akadora);
      int redS = tiles.Count(t => t.Suit == TileSuit.S && t.Akadora);

      switch (language) {
        case "zhs":
          sb.AppendLine($"- 牌组总张数：{total} 张（王牌 14 张）。");
          sb.AppendLine($"- 红五宝牌（每张和牌 +1 番）：{redM}x 0m，{redP}x 0p，{redS}x 0s。");
          break;
        case "zht":
          sb.AppendLine($"- 牌組總張數：{total} 張（王牌 14 張）。");
          sb.AppendLine($"- 紅五寶牌（每張和牌 +1 飜）：{redM}x 0m，{redP}x 0p，{redS}x 0s。");
          break;
        case "ja":
          sb.AppendLine($"- 総牌数: {total} 枚（王牌 14 枚）。");
          sb.AppendLine($"- 赤五ドラ（各 +1 飜）: {redM}x 0m, {redP}x 0p, {redS}x 0s。");
          break;
        default:
          sb.AppendLine($"- Total deck size: {total} tiles (dead wall: 14 tiles).");
          sb.AppendLine($"- Red fives (akadora, +1 bonus dora han each): {redM}x 0m, {redP}x 0p, {redS}x 0s.");
          break;
      }

      var missing = new List<string>();
      var nonStandard = new List<string>();
      foreach (var kind in AllStandardKinds) {
        int c = tiles.Count(t => t.WithoutDora == kind);
        string code = ShortKindCode(kind);
        if (c == 0) {
          missing.Add(code);
        } else if (c != 4) {
          nonStandard.Add($"{code}x{c}");
        }
      }

      if (missing.Count > 0) {
        var joined = string.Join(", ", missing);
        sb.AppendLine(language switch {
          "zhs" => $"- 牌组剔除牌种（0 张自然牌）：{joined}。",
          "zht" => $"- 牌組剔除牌種（0 張自然牌）：{joined}。",
          "ja" => $"- 除外されている牌種（山に0枚）: {joined}。",
          _ => $"- OMITTED tile kinds (0 natural copies in deck): {joined}.",
        });
      }
      if (nonStandard.Count > 0) {
        if (nonStandard.Count == AllStandardKinds.Length - missing.Count &&
            nonStandard.Select(s => s.Split('x')[1]).Distinct().Count() == 1) {
          string mult = nonStandard[0].Split('x')[1];
          sb.AppendLine(language switch {
            "zhs" => $"- 每种启用标准牌的张数：各 {mult} 张。",
            "zht" => $"- 每種啟用標準牌的張數：各 {mult} 張。",
            "ja" => $"- 各通常牌種の枚数: 各 {mult} 枚。",
            _ => $"- Copies per included standard tile kind: {mult} copies each.",
          });
        } else {
          var joined = string.Join(", ", nonStandard);
          sb.AppendLine(language switch {
            "zhs" => $"- 非标准牌种张数：{joined}。",
            "zht" => $"- 非標準牌種張數：{joined}。",
            "ja" => $"- 非標準枚数の牌種: {joined}。",
            _ => $"- Non-standard copy counts: {joined}.",
          });
        }
      } else if (missing.Count == 0) {
        sb.AppendLine(language switch {
          "zhs" => "- 标准 34 种牌：每种数牌与字牌各 4 张。",
          "zht" => "- 標準 34 種牌：每種數牌與字牌各 4 張。",
          "ja" => "- 標準34種牌: すべての数牌・字牌が各4枚。",
          _ => "- Standard 34 tile kinds: 4 copies of each suited and honor tile.",
        });
      }

      if (wildcards > 0) {
        switch (language) {
          case "zhs":
            sb.AppendLine($"- 已启用万能牌：牌组中含有 {wildcards}x `*` 万能牌！");
            sb.AppendLine("  * 替代规则：手牌或副露中的每张 `*` 可替代任意 34 种标准牌之一，用于组成顺子、刻子、杠子、雀头、七对子、国士无双、九莲宝灯或不塔役。结算时 `*` 自动采用使役种与打点最大化的牌面。");
            sb.AppendLine("  * `*` 的宝牌规则：当宝牌指示牌翻出 `*` 时，`*` 本身为宝牌（`* -> *`，每持有 1 张 `*` +1 番）；当普通指示牌使某牌（如 `5m`）成为宝牌时，用 `*` 替代 `5m` 不会继承 `5m` 的自然宝牌或红宝牌番数。");
            sb.AppendLine("  * 万能牌振听陷阱：持有 `*` 常会形成多面听甚至 34 面全牌待牌。只要你的所有听牌待牌中有任意一种曾出现在你自己的牌河中（或在同巡/立直后漏和），整副手牌即处于**振听**状态（禁止荣和，但仍可自摸）。");
            break;
          case "zht":
            sb.AppendLine($"- 已啟用萬能牌：牌組中含有 {wildcards}x `*` 萬能牌！");
            sb.AppendLine("  * 替代規則：手牌或副露中的每張 `*` 可替代任意 34 種標準牌之一，用於組成順子、刻子、槓子、雀頭、七對子、國士無雙、九蓮寶燈或不塔役。結算時 `*` 自動採用使役種與打點最大化的牌面。");
            sb.AppendLine("  * `*` 的寶牌規則：當寶牌指示牌翻出 `*` 時，`*` 本身為寶牌（`* -> *`，每持有 1 張 `*` +1 飜）；當普通指示牌使某牌（如 `5m`）成為寶牌時，用 `*` 替代 `5m` 不會繼承 `5m` 的自然寶牌或紅寶牌飜數。");
            sb.AppendLine("  * 萬能牌振聽陷阱：持有 `*` 常會形成多面聽甚至 34 面全牌待牌。只要你的所有聽牌待牌中有任意一種曾出現在你自己的牌河中（或在同巡/立直後漏和），整副手牌即處於**振聽**狀態（禁止榮和，但仍可自摸）。");
            break;
          case "ja":
            sb.AppendLine($"- オールマイティ牌有効: 山に {wildcards}x `*` 万能牌が含まれています！");
            sb.AppendLine("  * 代用ルール: 手牌や副露内の `*` は34種の通常牌のどれにでも代用でき、順子・刻子・槓子・雀頭・七対子・国士無双・九蓮宝燈・不塔役を構成できます。和了計算時は役・打点が最大になる牌として自動判定されます。");
            sb.AppendLine("  * `*` のドラ判定: ドラ表示牌が `*` の場合、`*` 自体がドラ（`* -> *`、所持1枚につき+1飜）になります。通常の表示牌で例えば `5m` がドラのとき、`*` を `5m` として代用しても `5m` の通常ドラ・赤ドラは乗りません。");
            sb.AppendLine("  * 万能牌のフリテン注意: `*` を持つと多面待ちや34種すべての牌待ちになることがあります。アガリ牌候補のうち1種類でも自分の河に捨てられている（または同巡・リーチ後に見逃した）場合、手牌全体が**フリテン**（ロン不可・ツモ和了のみ可）になります。");
            break;
          default:
            sb.AppendLine($"- WILDCARD TILES ENABLED: {wildcards}x `*` universal wildcard tiles are in the deck!");
            sb.AppendLine("  * Substitution: Each `*` in your hand or melds can stand for ANY of the 34 standard tiles to complete sequences, triplets, quads, pairs, Seven Pairs, Thirteen Orphans, Nine Gates, or Unconnected hands. At scoring, `*` automatically takes the assignment that maximizes yaku and points.");
            sb.AppendLine("  * Dora rules for `*`: When `*` is flipped as a dora indicator, `*` itself is dora (`* -> *`, +1 han per `*` held). When a normal indicator makes e.g. `5m` dora, using `*` to substitute for `5m` does NOT inherit `5m`'s natural dora/red-dora value.");
            sb.AppendLine("  * Wildcard Furiten trap: Holding `*` frequently creates multi-way or 34-way (`any-tile`) tenpai waits. If ANY tile among your tenpai winning waits is in your own discard river (or passed since your last draw/Riichi), your entire hand is FURITEN (cannot Ron; Tsumo win is still legal).");
            break;
        }
      } else {
        sb.AppendLine(language switch {
          "zhs" => "- 万能牌（`*`）：0 张（本局无万能牌）。",
          "zht" => "- 萬能牌（`*`）：0 張（本局無萬能牌）。",
          "ja" => "- オールマイティ牌（`*`）: 0 枚（本対局では使用しません）。",
          _ => "- Wildcard (`*`) tiles: 0 (no wildcards in this match).",
        });
      }

      return sb.ToString().TrimEnd();
    }

    public string BuildGameRules(PublicGameView view) {
      var sb = new StringBuilder();
      bool aotenjou = !HasStandardYakumanCap(view.ScoringOptions);

      switch (language) {
        case "zhs": {
          var length = view.TotalRound <= 1 ? "东风战（1 圈）" : "东南战 / 半庄战（2 圈）";
          sb.AppendLine($"- 对局赛制：{view.PlayerCount} 人 {length}。");
          sb.AppendLine($"- 点数设定：起始 {view.InitialPoints}，原点/目标 {view.FinishPoints}，立直棒 {view.RiichiPoints}，本场棒合计 +{view.HonbaPoints}。");
          sb.AppendLine($"- 最低起和番数（`minHan`）：{view.MinHan} 番（宝牌/红宝牌/里宝牌/拔北宝牌绝不计入 `minHan`）。");
          sb.AppendLine($"- 食断（副露断幺九）：{(view.AllowsOpenTanyao ? "允许" : "禁止（断幺九仅限门前清）")}。");
          if (view.AllowsNukidora) {
            sb.AppendLine("- 拔北宝牌（`4z`）：已启用——拔出 `4z` 记 +1 奖励宝牌番并摸岭上牌。");
          }
          sb.AppendLine(aotenjou
              ? "- 计分上限：**青天井（AOTENJOU）**——无役满封顶，按 `符 * 2^(番+2)` 指数计分，高番牌可达数十万点！"
              : $"- 计分选项：{view.ScoringOptions}。");
          sb.AppendLine($"- 宝牌选项：{view.DoraOptions}。");
          sb.AppendLine($"- 和牌选项：{view.AgariOptions}。");
          sb.AppendLine($"- 立直规则：{view.RiichiPolicy}；食替禁手：{view.KuikaePolicy}。");
          sb.AppendLine($"- 连庄规则：{view.RenchanPolicy}；终局规则：{view.EndGamePolicy}。");
          sb.AppendLine($"- 中途流局：{view.RyuukyokuTriggers}。");
          break;
        }
        case "zht": {
          var length = view.TotalRound <= 1 ? "東風戰（1 圈）" : "東南戰 / 半莊戰（2 圈）";
          sb.AppendLine($"- 對局賽制：{view.PlayerCount} 人 {length}。");
          sb.AppendLine($"- 點數設定：起始 {view.InitialPoints}，原點/目標 {view.FinishPoints}，立直棒 {view.RiichiPoints}，本場棒合計 +{view.HonbaPoints}。");
          sb.AppendLine($"- 最低起和飜數（`minHan`）：{view.MinHan} 飜（寶牌/紅寶牌/裡寶牌/拔北寶牌絕不計入 `minHan`）。");
          sb.AppendLine($"- 食斷（副露斷幺九）：{(view.AllowsOpenTanyao ? "允許" : "禁止（斷幺九僅限門前清）")}。");
          if (view.AllowsNukidora) {
            sb.AppendLine("- 拔北寶牌（`4z`）：已啟用——拔出 `4z` 記 +1 獎勵寶牌飜並摸嶺上牌。");
          }
          sb.AppendLine(aotenjou
              ? "- 計分上限：**青天井（AOTENJOU）**——無役滿封頂，按 `符 * 2^(飜+2)` 指數計分，高飜牌可達數十萬點！"
              : $"- 計分選項：{view.ScoringOptions}。");
          sb.AppendLine($"- 寶牌選項：{view.DoraOptions}。");
          sb.AppendLine($"- 和牌選項：{view.AgariOptions}。");
          sb.AppendLine($"- 立直規則：{view.RiichiPolicy}；食替禁手：{view.KuikaePolicy}。");
          sb.AppendLine($"- 連莊規則：{view.RenchanPolicy}；終局規則：{view.EndGamePolicy}。");
          sb.AppendLine($"- 中途流局：{view.RyuukyokuTriggers}。");
          break;
        }
        case "ja": {
          var length = view.TotalRound <= 1 ? "東風戦（1場）" : "東南戦 / 半荘戦（2場）";
          sb.AppendLine($"- 対局形式: {view.PlayerCount}人打ち {length}。");
          sb.AppendLine($"- 点数設定: 開始 {view.InitialPoints} 点、返し/基準 {view.FinishPoints} 点、供託リーチ棒 {view.RiichiPoints} 点、本場 +{view.HonbaPoints} 点。");
          sb.AppendLine($"- 最低必要飜数（`minHan`）: {view.MinHan} 飜（ドラ・赤ドラ・裏ドラ・抜きドラは `minHan` に含まれません）。");
          sb.AppendLine($"- 喰いタン: {(view.AllowsOpenTanyao ? "あり" : "なし（断幺九は門前限定）")}。");
          if (view.AllowsNukidora) {
            sb.AppendLine("- 抜きドラ（`4z` 北抜き）: あり — `4z` を抜くとドラ+1飜となり嶺上牌をツモります。");
          }
          sb.AppendLine(aotenjou
              ? "- 点数上限: **青天井（AOTENJOU）** — 役満上限なしの指数計算 `符 * 2^(飜+2)` のため、高飜手は数万〜数十万点になります！"
              : $"- 計算オプション: {view.ScoringOptions}。");
          sb.AppendLine($"- ドラ設定: {view.DoraOptions}。");
          sb.AppendLine($"- 和了設定: {view.AgariOptions}。");
          sb.AppendLine($"- リーチ制限: {view.RiichiPolicy} / 喰い替え禁止: {view.KuikaePolicy}。");
          sb.AppendLine($"- 連荘ルール: {view.RenchanPolicy} / 終局ルール: {view.EndGamePolicy}。");
          sb.AppendLine($"- 途中流局: {view.RyuukyokuTriggers}。");
          break;
        }
        default: {
          var length = view.TotalRound <= 1
              ? "East-only (Tonpuusen, 1 wind round)"
              : "East + South (Hanchan, 2 wind rounds)";
          sb.AppendLine($"- Match format: {view.PlayerCount}-player {length}.");
          sb.AppendLine($"- Points: start {view.InitialPoints}, target to finish {view.FinishPoints}, riichi deposit {view.RiichiPoints}, honba +{view.HonbaPoints} total.");
          sb.AppendLine($"- Minimum yaku requirement (`minHan`): {view.MinHan} han (dora/red-five/ura-dora/nukidora NEVER count toward `minHan`).");
          sb.AppendLine($"- Open Tanyao (Kuitan): {(view.AllowsOpenTanyao ? "Allowed" : "Forbidden (Tanyao requires a closed hand)")}.");
          if (view.AllowsNukidora) {
            sb.AppendLine("- Nukidora (`4z` North pull): Enabled — extracting `4z` sets it aside for +1 bonus dora han and draws a dead-wall replacement tile.");
          }
          if (aotenjou) {
            sb.AppendLine("- Scoring cap: AOTENJOU (Uncapped exponential `fu * 2^(han+2)` scoring — high han hands explode into tens/hundreds of thousands of points!).");
          } else {
            sb.AppendLine($"- Scoring options: {view.ScoringOptions}.");
          }
          sb.AppendLine($"- Dora options: {view.DoraOptions}.");
          sb.AppendLine($"- Agari options: {view.AgariOptions}.");
          sb.AppendLine($"- Riichi policy: {view.RiichiPolicy}; Swap-calling (Kuikae) ban: {view.KuikaePolicy}.");
          sb.AppendLine($"- Dealer repeat (Renchan): {view.RenchanPolicy}; Match end policy: {view.EndGamePolicy}.");
          sb.AppendLine($"- Abortive draw triggers: {view.RyuukyokuTriggers}.");
          break;
        }
      }

      var yaku = view.AllowedYakus ?? [];
      if (yaku.Count > 0) {
        var standard = new List<string>();
        var koyaku = new List<ArenaMetadata.YakuCatalogItem>();
        foreach (var name in yaku) {
          if (ArenaMetadata.YakuByName.TryGetValue(name, out var item) && item.Group == "koyaku") {
            koyaku.Add(item);
          } else {
            standard.Add(name);
          }
        }
        sb.AppendLine(language switch {
          "zhs" => $"- 已启用标准役种（{standard.Count} 种）：{string.Join(", ", standard)}。",
          "zht" => $"- 已啟用標準役種（{standard.Count} 種）：{string.Join(", ", standard)}。",
          "ja" => $"- 有効な通常役（{standard.Count} 種）: {string.Join(", ", standard)}。",
          _ => $"- Enabled standard yaku ({standard.Count}): {string.Join(", ", standard)}.",
        });
        if (koyaku.Count > 0) {
          sb.AppendLine(language switch {
            "zhs" => $"- 已启用古役 / Koyaku（{koyaku.Count} 种）：",
            "zht" => $"- 已啟用古役 / Koyaku（{koyaku.Count} 種）：",
            "ja" => $"- 有効な古役 / Koyaku（{koyaku.Count} 種）:",
            _ => $"- Enabled Classical Yaku / Koyaku ({koyaku.Count}):",
          });
          foreach (var k in koyaku) {
            if (language != "en" && KoyakuPromptText.TryGetValue(k.Name, out var loc)) {
              string detail = language switch {
                "zhs" => loc.Zhs,
                "zht" => loc.Zht,
                "ja" => loc.Ja,
                _ => $"{k.Name} [{k.HanSummary}]: {k.Description}",
              };
              sb.AppendLine($"  * {k.Name} — {detail}");
            } else {
              sb.AppendLine($"  * {k.Name} [{k.HanSummary}]: {k.Description}");
            }
          }
        } else {
          sb.AppendLine(language switch {
            "zhs" => "- 古役（Koyaku）：未启用。",
            "zht" => "- 古役（Koyaku）：未啟用。",
            "ja" => "- 古役（Koyaku）: なし。",
            _ => "- Classical Yaku (Koyaku): None enabled.",
          });
        }
      }
      return sb.ToString().TrimEnd();
    }

    public static string RoundLabelOf(PublicGameView view) =>
        $"{WindLabel(view.RoundWind, "en")} {view.Round % Math.Max(1, view.PlayerCount) + 1}" +
        (view.Honba > 0 ? $" (honba {view.Honba})" : "");

    public static string RoundLabelOf(PublicGameView view, string language) {
      var lang = NormalizeArenaLanguage(language);
      int num = view.Round % Math.Max(1, view.PlayerCount) + 1;
      return lang switch {
        "zhs" => $"{WindLabel(view.RoundWind, "zhs")}{num}局" + (view.Honba > 0 ? $"（{view.Honba}本场）" : ""),
        "zht" => $"{WindLabel(view.RoundWind, "zht")}{num}局" + (view.Honba > 0 ? $"（{view.Honba}本場）" : ""),
        "ja" => $"{WindLabel(view.RoundWind, "ja")}{num}局" + (view.Honba > 0 ? $"（{view.Honba}本場）" : ""),
        _ => RoundLabelOf(view),
      };
    }

    public string BuildTurnMessage(
        PublicGameView view,
        IReadOnlyList<LlmChoice> menu,
        IReadOnlyList<string> recentEvents = null,
        IReadOnlyList<ArenaChatLine> chats = null,
        string validationError = null) {
      var sb = new StringBuilder();

      if (!string.IsNullOrEmpty(validationError)) {
        switch (language) {
          case "zhs":
            sb.AppendLine($"警告 — 你上一次的回复已被拒绝：{validationError}");
            sb.AppendLine("请严格从下方合法动作菜单中选择一个有效的整数动作编号。");
            break;
          case "zht":
            sb.AppendLine($"警告 — 你上一次的回覆已被拒絕：{validationError}");
            sb.AppendLine("請嚴格從下方合法動作選單中選擇一個有效的整數動作編號。");
            break;
          case "ja":
            sb.AppendLine($"警告 — 直前の応答は無効として却下されました: {validationError}");
            sb.AppendLine("以下の合法アクション一覧から有効な整数IDを1つだけ選んでください。");
            break;
          default:
            sb.AppendLine($"WARNING — Your previous response was REJECTED: {validationError}");
            sb.AppendLine("Select a valid numeric action id strictly from the menu below.");
            break;
        }
        sb.AppendLine();
      }

      if (recentEvents is { Count: > 0 }) {
        sb.AppendLine(language switch {
          "zhs" => "近期牌桌事件：",
          "zht" => "近期牌桌事件：",
          "ja" => "直近の卓上イベント:",
          _ => "Recent table events:",
        });
        foreach (var line in recentEvents) {
          sb.Append("  - ").AppendLine(line);
        }
        sb.AppendLine();
      }

      AppendChats(sb, chats);
      AppendGameState(sb, view);
      AppendOpponents(sb, view);

      string mode = (promptConfig.ToolContextMode ?? "standard").Trim().ToLowerInvariant();
      if (mode != "minimal") {
        AppendToolContext(sb, view, extended: mode == "extended");
      }

      AppendMenu(sb, menu);
      sb.AppendLine();
      sb.AppendLine(BuildTurnFooterJsonHint());
      return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Compact 1-2 line summary of a completed decision turn for bounded history retention,
    /// avoiding multi-thousand-token board repetition across long matches.
    /// </summary>
    public string BuildCondensedTurnSummary(PublicGameView view, string chosenAction) {
      var hand = DescribeSelfHand(view);
      return language switch {
        "zhs" => $"[{RoundLabelOf(view, language)}，巡目 {view.SelfJun}，余牌 {view.WallRemaining}，点数 {view.PointsOf(view.Seat)}] 手牌: {hand} -> 选择: {chosenAction}",
        "zht" => $"[{RoundLabelOf(view, language)}，巡目 {view.SelfJun}，餘牌 {view.WallRemaining}，點數 {view.PointsOf(view.Seat)}] 手牌: {hand} -> 選擇: {chosenAction}",
        "ja" => $"[{RoundLabelOf(view, language)}、{view.SelfJun}巡目、山残り{view.WallRemaining}、持ち点{view.PointsOf(view.Seat)}] 手牌: {hand} -> 選択: {chosenAction}",
        _ => $"[{RoundLabelOf(view)}, jun {view.SelfJun}, wall {view.WallRemaining}, pts {view.PointsOf(view.Seat)}] Hand: {hand} -> Chose: {chosenAction}",
      };
    }

    private void AppendGameState(StringBuilder sb, PublicGameView view) {
      int roundNum = view.Round % Math.Max(1, view.PlayerCount) + 1;
      switch (language) {
        case "zhs": {
          sb.AppendLine("== 当前牌桌状态 ==");
          string allLast = view.IsAllLast ? " [ALL-LAST]" : "";
          sb.AppendLine(
              $"局次: {WindLabel(view.RoundWind, language)}{roundNum}局{allLast} | " +
              $"本场: {view.Honba} | 场供立直棒: {view.RiichiStick}（{view.RiichiStick * view.RiichiPoints} 点） | " +
              $"牌山余牌: {view.WallRemaining} | 当前巡目: {view.SelfJun}");
          string leadStr = view.SelfPointLead >= 0
              ? $"领先次位 +{view.SelfPointLead}"
              : $"落后头名 {view.SelfPointLead}";
          sb.AppendLine(
              $"你的点数: {view.PointsOf(view.Seat)} 点（顺位 #{view.SelfRank}/{view.PlayerCount}，{leadStr}） | " +
              $"自风: {WindLabel(view.SelfWind, language)}{(view.SelfIsDealer ? " [庄家]" : "")}" +
              (view.SelfRiichi ? " [已立直]" : ""));
          break;
        }
        case "zht": {
          sb.AppendLine("== 當前牌桌狀態 ==");
          string allLast = view.IsAllLast ? " [ALL-LAST]" : "";
          sb.AppendLine(
              $"局次: {WindLabel(view.RoundWind, language)}{roundNum}局{allLast} | " +
              $"本場: {view.Honba} | 場供立直棒: {view.RiichiStick}（{view.RiichiStick * view.RiichiPoints} 點） | " +
              $"牌山餘牌: {view.WallRemaining} | 當前巡目: {view.SelfJun}");
          string leadStr = view.SelfPointLead >= 0
              ? $"領先次位 +{view.SelfPointLead}"
              : $"落後頭名 {view.SelfPointLead}";
          sb.AppendLine(
              $"你的點數: {view.PointsOf(view.Seat)} 點（順位 #{view.SelfRank}/{view.PlayerCount}，{leadStr}） | " +
              $"自風: {WindLabel(view.SelfWind, language)}{(view.SelfIsDealer ? " [莊家]" : "")}" +
              (view.SelfRiichi ? " [已立直]" : ""));
          break;
        }
        case "ja": {
          sb.AppendLine("== 現在の卓上状態 ==");
          string allLast = view.IsAllLast ? " [オーラス]" : "";
          sb.AppendLine(
              $"局: {WindLabel(view.RoundWind, language)}{roundNum}局{allLast} | " +
              $"本場: {view.Honba} | 供託リーチ棒: {view.RiichiStick}（{view.RiichiStick * view.RiichiPoints}点） | " +
              $"山残り: {view.WallRemaining} | 自分の巡目: {view.SelfJun}");
          string leadStr = view.SelfPointLead >= 0
              ? $"2位と+{view.SelfPointLead}点差"
              : $"トップと{view.SelfPointLead}点差";
          sb.AppendLine(
              $"持ち点: {view.PointsOf(view.Seat)}点（現在 #{view.SelfRank}/{view.PlayerCount}位、{leadStr}） | " +
              $"自風: {WindLabel(view.SelfWind, language)}{(view.SelfIsDealer ? " [親]" : "")}" +
              (view.SelfRiichi ? " [リーチ中]" : ""));
          break;
        }
        default: {
          sb.AppendLine("== Current Table State ==");
          string allLast = view.IsAllLast ? " [ALL-LAST]" : "";
          sb.AppendLine(
              $"Round: {WindLabel(view.RoundWind, language)} {roundNum}{allLast} | " +
              $"Honba: {view.Honba} | Riichi sticks on table: {view.RiichiStick} ({view.RiichiStick * view.RiichiPoints} pts) | " +
              $"Live wall remaining: {view.WallRemaining} | Your turn (jun): {view.SelfJun}");
          string leadStr = view.SelfPointLead >= 0
              ? $"+{view.SelfPointLead} vs next"
              : $"{view.SelfPointLead} behind leader";
          sb.AppendLine(
              $"Your score: {view.PointsOf(view.Seat)} pts (Rank #{view.SelfRank}/{view.PlayerCount}, {leadStr}) | " +
              $"Seat wind: {WindLabel(view.SelfWind, language)}{(view.SelfIsDealer ? " [DEALER]" : "")}" +
              (view.SelfRiichi ? " [IN RIICHI]" : ""));
          break;
        }
      }

      var indicators = view.RevealedDoraIndicators;
      if (indicators.Count == 0) {
        sb.AppendLine(language switch {
          "zhs" => "宝牌指示牌: 暂无翻开",
          "zht" => "寶牌指示牌: 暫無翻開",
          "ja" => "ドラ表示牌: なし",
          _ => "Dora indicators: none revealed",
        });
      } else {
        var indStr = string.Join(", ", indicators.Select(t => TileNotation.One(t, language)));
        var activeStr = string.Join(", ", indicators.Select(ind => {
          var dora = ind.NextDora;
          int inDeck = view.DeckCount(dora);
          if (inDeck == 0) {
            return language switch {
              "zhs" => $"{TileNotation.One(dora, language)} (牌组中0张自然牌)",
              "zht" => $"{TileNotation.One(dora, language)} (牌組中0張自然牌)",
              "ja" => $"{TileNotation.One(dora, language)} (山に自然牌0枚)",
              _ => $"{TileNotation.One(dora, language)} (0 natural copies in deck)",
            };
          }
          return language switch {
            "zhs" => $"{TileNotation.One(dora, language)} (未见 {view.UnseenCount(dora)}/{inDeck})",
            "zht" => $"{TileNotation.One(dora, language)} (未見 {view.UnseenCount(dora)}/{inDeck})",
            "ja" => $"{TileNotation.One(dora, language)} (未見 {view.UnseenCount(dora)}/{inDeck})",
            _ => $"{TileNotation.One(dora, language)} ({view.UnseenCount(dora)}/{inDeck} unseen)",
          };
        }));
        sb.AppendLine(language switch {
          "zhs" => $"宝牌指示牌: {indStr}  =>  当前宝牌: {activeStr}",
          "zht" => $"寶牌指示牌: {indStr}  =>  當前寶牌: {activeStr}",
          "ja" => $"ドラ表示牌: {indStr}  =>  有効ドラ牌: {activeStr}",
          _ => $"Dora indicator(s): {indStr}  =>  Active dora tile(s): {activeStr}",
        });
      }

      if (view.AllowsNukidora || view.SelfNukiDoraCount > 0) {
        sb.AppendLine(language switch {
          "zhs" => $"你的拔北宝牌数: {view.SelfNukiDoraCount}",
          "zht" => $"你的拔北寶牌數: {view.SelfNukiDoraCount}",
          "ja" => $"自分の抜きドラ数: {view.SelfNukiDoraCount}",
          _ => $"Your pulled North (nukidora): {view.SelfNukiDoraCount}",
        });
      }

      sb.Append(language switch {
        "zhs" => "你的手牌: ",
        "zht" => "你的手牌: ",
        "ja" => "自分の手牌: ",
        _ => "Your hand: ",
      }).AppendLine(DescribeSelfHand(view));

      sb.Append(language switch {
        "zhs" => "你的牌河 (最早 -> 最新): ",
        "zht" => "你的牌河 (最早 -> 最新): ",
        "ja" => "自分の捨て牌 (古い順 -> 新しい順): ",
        _ => "Your discard river (oldest -> newest): ",
      }).AppendLine(FormatChronologicalRiver(view.DiscardsOf(view.Seat), view.RiichiTileOf(view.Seat)));
    }

    private void AppendOpponents(StringBuilder sb, PublicGameView view) {
      sb.AppendLine(language switch {
        "zhs" => "== 对手状态 ==",
        "zht" => "== 對手狀態 ==",
        "ja" => "== 他家の状態 ==",
        _ => "== Opponents ==",
      });
      foreach (var seat in view.OpponentSeats.OrderBy(s => s)) {
        var flags = new List<string>();
        if (view.IsDealer(seat)) flags.Add(language switch { "zhs" => "庄家", "zht" => "莊家", "ja" => "親", _ => "DEALER" });
        if (view.IsDoubleRiichi(seat)) flags.Add(language switch { "zhs" => "双立直", "zht" => "雙立直", "ja" => "ダブル立直", _ => "DOUBLE-RIICHI" });
        else if (view.IsRiichi(seat)) flags.Add(language switch { "zhs" => "立直", "zht" => "立直", "ja" => "リーチ", _ => "RIICHI" });
        if (view.IsIppatsu(seat)) flags.Add(language switch { "zhs" => "一发圈", "zht" => "一發圈", "ja" => "一発圏", _ => "IPPATSU" });
        int visDora = view.VisibleDoraOf(seat);
        if (visDora > 0) {
          flags.Add(language switch {
            "zhs" => $"明牌宝牌 {visDora}",
            "zht" => $"明牌寶牌 {visDora}",
            "ja" => $"公開ドラ {visDora}",
            _ => $"{visDora} visible open dora",
          });
        }
        int nuki = view.NukiDoraCountOf(seat);
        if (nuki > 0) {
          flags.Add(language switch {
            "zhs" => $"拔北 {nuki}",
            "zht" => $"拔北 {nuki}",
            "ja" => $"抜きドラ {nuki}",
            _ => $"{nuki} nukidora",
          });
        }

        string flagSuffix = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";
        sb.AppendLine(language switch {
          "zhs" => $"  {labeler.Label(seat)}: {view.PointsOf(seat)} 点 (顺位 #{view.RankOf(seat)}), {WindLabel(view.WindOf(seat), language)}风, 巡目 {view.JunOf(seat)}{flagSuffix}",
          "zht" => $"  {labeler.Label(seat)}: {view.PointsOf(seat)} 點 (順位 #{view.RankOf(seat)}), {WindLabel(view.WindOf(seat), language)}風, 巡目 {view.JunOf(seat)}{flagSuffix}",
          "ja" => $"  {labeler.Label(seat)}: {view.PointsOf(seat)}点 (#{view.RankOf(seat)}位), {WindLabel(view.WindOf(seat), language)}家, {view.JunOf(seat)}巡目{flagSuffix}",
          _ => $"  {labeler.Label(seat)}: {view.PointsOf(seat)} pts (Rank #{view.RankOf(seat)}), {WindLabel(view.WindOf(seat), language)} wind, jun {view.JunOf(seat)}{flagSuffix}",
        });

        var called = view.CalledOf(seat);
        if (called.Count > 0) {
          sb.Append(language switch {
            "zhs" => "    副露/杠: ",
            "zht" => "    副露/槓: ",
            "ja" => "    副露/槓: ",
            _ => "    open/kan melds: ",
          }).AppendLine(string.Join(" ; ", called.Select(m => DescribeMeld(m, view))));
        }
        sb.Append(language switch {
          "zhs" or "zht" => "    牌河: ",
          "ja" => "    河: ",
          _ => "    river: ",
        }).AppendLine(FormatChronologicalRiver(view.DiscardsOf(seat), view.RiichiTileOf(seat)));
      }
    }

    private void AppendToolContext(StringBuilder sb, PublicGameView view, bool extended) {
      sb.AppendLine(language switch {
        "zhs" => "== 预计算公开辅助分析 ==",
        "zht" => "== 預計算公開輔助分析 ==",
        "ja" => "== 公開情報に基づく事前解析 ==",
        _ => "== Pre-computed Public Analysis ==",
      });
      var selfShanten = SelfShanten(view);
      switch (language) {
        case "zhs":
          sb.Append("  手牌状态: ");
          sb.Append(selfShanten == int.MaxValue ? "向听数未知"
              : selfShanten < 0 ? "已和牌形"
              : selfShanten == 0 ? "听牌（0向听）"
              : $"{selfShanten}向听");
          sb.Append(view.SelfMenzen ? "，门前清" : "，已副露");
          if (view.SelfRiichi) sb.Append("，立直锁定中");
          if (view.SelfFuriten) sb.Append("，【振听】（禁止荣和，仅可自摸！）");
          sb.AppendLine("。");
          break;
        case "zht":
          sb.Append("  手牌狀態: ");
          sb.Append(selfShanten == int.MaxValue ? "向聽數未知"
              : selfShanten < 0 ? "已和牌形"
              : selfShanten == 0 ? "聽牌（0向聽）"
              : $"{selfShanten}向聽");
          sb.Append(view.SelfMenzen ? "，門前清" : "，已副露");
          if (view.SelfRiichi) sb.Append("，立直鎖定中");
          if (view.SelfFuriten) sb.Append("，【振聽】（禁止榮和，僅可自摸！）");
          sb.AppendLine("。");
          break;
        case "ja":
          sb.Append("  手牌状態: ");
          sb.Append(selfShanten == int.MaxValue ? "向聴数不明"
              : selfShanten < 0 ? "和了形完成"
              : selfShanten == 0 ? "聴牌（0向聴）"
              : $"{selfShanten}向聴");
          sb.Append(view.SelfMenzen ? "、門前" : "、副露済み");
          if (view.SelfRiichi) sb.Append("、リーチ固定");
          if (view.SelfFuriten) sb.Append("、【フリテン】（ロン不可・ツモのみ！）");
          sb.AppendLine("。");
          break;
        default:
          sb.Append("  Hand status: ");
          sb.Append(selfShanten == int.MaxValue
              ? "shanten unavailable"
              : selfShanten < 0 ? "COMPLETE (winning shape)"
              : selfShanten == 0 ? "TENPAI (0-shanten)"
              : $"{selfShanten}-shanten");
          sb.Append(view.SelfMenzen ? ", concealed (menzen)" : ", open (called)");
          if (view.SelfRiichi) sb.Append(", locked in Riichi");
          if (view.SelfFuriten) sb.Append(", FURITEN (cannot Ron — Tsumo only!)");
          sb.AppendLine(".");
          break;
      }

      var tenpai = view.SelfTenpaiInfos();
      if (tenpai.Count > 0) {
        sb.Append(language switch {
          "zhs" => "  当前 13 张基底待牌: ",
          "zht" => "  當前 13 張基底待牌: ",
          "ja" => "  現在の13枚形での待ち牌: ",
          _ => "  Current 13-tile base waits: ",
        });
        if (tenpai.Count > 12) {
          int totalLive = tenpai.Sum(info => view.UnseenCount(info.winningTile));
          var best = tenpai.OrderByDescending(t => t.yakuman * 100 + t.han).First();
          sb.AppendLine(language switch {
            "zhs" => $"{tenpai.Count} 种牌（未见合计 {totalLive} 枚；最高 {(best.yakuman > 0 ? $"{best.yakuman}倍役满" : $"{best.han}番（役{best.yaku}番）")}）。",
            "zht" => $"{tenpai.Count} 種牌（未見合計 {totalLive} 枚；最高 {(best.yakuman > 0 ? $"{best.yakuman}倍役滿" : $"{best.han}飜（役{best.yaku}飜）")}）。",
            "ja" => $"{tenpai.Count} 種（未見計 {totalLive} 枚；最大 {(best.yakuman > 0 ? $"{best.yakuman}倍役満" : $"{best.han}飜（役{best.yaku}飜）")}）。",
            _ => $"{tenpai.Count} distinct tile kinds ({totalLive} unseen copies total; {(best.yakuman > 0 ? $"{best.yakuman} yakuman" : $"up to {best.han} han ({best.yaku} yaku han)")}).",
          });
        } else {
          sb.AppendLine(string.Join(", ", tenpai.Select(info => {
            var unseen = view.UnseenCount(info.winningTile);
            var tileStr = TileNotation.One(info.winningTile, language);
            return language switch {
              "zhs" => $"{tileStr} [{(info.yakuman > 0 ? $"{info.yakuman}倍役满" : $"{info.han}番(役{info.yaku}番) {info.fu}符")}; 未见 {unseen}]",
              "zht" => $"{tileStr} [{(info.yakuman > 0 ? $"{info.yakuman}倍役滿" : $"{info.han}飜(役{info.yaku}飜) {info.fu}符")}; 未見 {unseen}]",
              "ja" => $"{tileStr} [{(info.yakuman > 0 ? $"{info.yakuman}倍役満" : $"{info.han}飜(役{info.yaku}飜) {info.fu}符")}; 未見 {unseen}]",
              _ => $"{tileStr} [{(info.yakuman > 0 ? $"{info.yakuman} yakuman" : $"{info.han} han ({info.yaku} yaku han), {info.fu} fu")}; {unseen} unseen]",
            };
          })));
        }
      }

      // 100% safe genbutsu tiles in hand against each Riichi opponent.
      foreach (var opp in view.OpponentSeats.Where(view.IsRiichi).OrderBy(s => s)) {
        var safeInHand = FindGenbutsuInHand(view, opp);
        string noneWord = language switch { "zhs" or "zht" => "无", "ja" => "なし", _ => "NONE" };
        sb.Append(language switch {
          "zhs" => $"  手牌中对 {labeler.Label(opp)} 100% 安全现物: ",
          "zht" => $"  手牌中對 {labeler.Label(opp)} 100% 安全現物: ",
          "ja" => $"  {labeler.Label(opp)} に対する手牌内の現物(100%安牌): ",
          _ => $"  100% safe (genbutsu) tiles in your hand vs {labeler.Label(opp)}: ",
        });
        sb.AppendLine(safeInHand.Count == 0 ? noneWord : string.Join(", ", safeInHand));
      }

      int totalWildcards = view.DeckCount(Tile.Wildcard);
      if (totalWildcards > 0) {
        sb.AppendLine(language switch {
          "zhs" => $"  未见万能牌 `*`: {view.UnseenCount(Tile.Wildcard)}/{totalWildcards}。",
          "zht" => $"  未見萬能牌 `*`: {view.UnseenCount(Tile.Wildcard)}/{totalWildcards}。",
          "ja" => $"  未見の万能牌 `*`: {view.UnseenCount(Tile.Wildcard)}/{totalWildcards}。",
          _ => $"  Wildcards `*` unseen: {view.UnseenCount(Tile.Wildcard)}/{totalWildcards}.",
        });
      }

      if (extended) {
        sb.AppendLine(language switch {
          "zhs" => "  各花色未见张数:",
          "zht" => "  各花色未見張數:",
          "ja" => "  スート別の未見枚数:",
          _ => "  Unseen tile counts by suit:",
        });
        foreach (var (suitNameEn, suitNameZhs, suitNameZht, suitNameJa, suit) in new[] {
          ("Manzu", "万子", "萬子", "萬子", TileSuit.M),
          ("Pinzu", "筒子", "筒子", "筒子", TileSuit.P),
          ("Souzu", "索子", "索子", "索子", TileSuit.S),
        }) {
          var parts = new List<string>();
          for (int n = 1; n <= 9; n++) {
            var t = new Tile(suit, n);
            int deck = view.DeckCount(t);
            if (deck > 0) {
              parts.Add($"{n}{SuitLetter(suit)}:{view.UnseenCount(t)}");
            }
          }
          if (parts.Count > 0) {
            string sLabel = language switch {
              "zhs" => suitNameZhs,
              "zht" => suitNameZht,
              "ja" => suitNameJa,
              _ => suitNameEn,
            };
            sb.Append("    ").Append(sLabel).Append(": ").AppendLine(string.Join(" ", parts));
          }
        }
        var honorParts = new List<string>();
        for (int z = 1; z <= 7; z++) {
          var t = new Tile(TileSuit.Z, z);
          if (view.DeckCount(t) > 0) {
            honorParts.Add($"{z}z:{view.UnseenCount(t)}");
          }
        }
        if (honorParts.Count > 0) {
          string hLabel = language switch {
            "zhs" or "zht" or "ja" => "字牌",
            _ => "Honors",
          };
          sb.Append("    ").Append(hLabel).Append(": ").AppendLine(string.Join(" ", honorParts));
        }
      }
    }

    private List<string> FindGenbutsuInHand(PublicGameView view, int riichiSeat) {
      var safeKinds = new HashSet<Tile>();
      foreach (var d in view.DiscardsOf(riichiSeat)) {
        if (!d.tile.IsWildcard) {
          safeKinds.Add(d.tile.WithoutDora);
        }
      }
      int riichiTime = view.RiichiTimeOf(riichiSeat);
      if (riichiTime >= 0) {
        foreach (var seat in view.AllSeats) {
          foreach (var d in view.DiscardsOf(seat)) {
            if (!d.tile.IsWildcard && d.discardInfo != null && d.discardInfo.time >= riichiTime) {
              safeKinds.Add(d.tile.WithoutDora);
            }
          }
        }
      }

      var held = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        held.Add(view.SelfHand.pendingTile);
      }
      return held
          .Where(gt => !gt.tile.IsWildcard && safeKinds.Contains(gt.tile.WithoutDora))
          .Select(gt => TileNotation.One(gt.tile, language))
          .Distinct()
          .ToList();
    }

    private string FormatChronologicalRiver(IReadOnlyList<GameTile> discards, GameTile riichiTile) {
      if (discards == null || discards.Count == 0) {
        return language switch {
          "zhs" or "zht" => "无",
          "ja" => "なし",
          _ => "none",
        };
      }
      var parts = new List<string>(discards.Count);
      foreach (var gt in discards) {
        var token = TileNotation.One(gt, language);
        if (riichiTile != null && ReferenceEquals(gt, riichiTile)) {
          token += "[riichi]";
        }
        if (gt.source != TileSource.Discard) {
          token += "(called)";
        }
        parts.Add(token);
      }
      return string.Join(" ", parts);
    }

    private void AppendMenu(StringBuilder sb, IReadOnlyList<LlmChoice> menu) {
      sb.AppendLine(language switch {
        "zhs" => "== 合法动作菜单（仅选择一个整数编号） ==",
        "zht" => "== 合法動作選單（僅選擇一個整數編號） ==",
        "ja" => "== 合法アクション一覧（整数IDを1つ選択） ==",
        _ => "== Legal Actions (choose ONE numeric id) ==",
      });
      foreach (var choice in menu) {
        sb.Append("  [").Append(choice.Id).Append("] ").AppendLine(choice.Description);
      }
    }

    private void AppendChats(StringBuilder sb, IReadOnlyList<ArenaChatLine> chats) {
      if (chats == null || chats.Count == 0) {
        return;
      }
      sb.AppendLine(language switch {
        "zhs" => "上次轮到你以来的牌桌发言：",
        "zht" => "上次輪到你以來的牌桌發言：",
        "ja" => "前回の手番以降の卓上チャット:",
        _ => "Table chat since your last turn:",
      });
      foreach (var chat in chats) {
        sb.Append("  - ").Append(chat.Sender).Append(": ").AppendLine(chat.Text);
      }
      sb.AppendLine();
    }

    private string DescribeMeld(MenLike meld, PublicGameView view) {
      var body = $"{MeldTypeLabel(meld)} {TileNotation.Meld(meld, language)}";
      var claimed = meld.FirstOrDefault(t => !t.IsTsumo && t.discardInfo != null);
      if (claimed == null) {
        return body;
      }
      var src = SourceLabel(claimed.discardInfo.from, view);
      var tile = TileNotation.One(claimed, language);
      return language switch {
        "zhs" or "zht" => $"{body} ({tile} 鸣自 {src})",
        "ja" => $"{body} ({src} から {tile})",
        _ => $"{body} ({tile} from {src})",
      };
    }

    private static string MeldTypeLabel(MenLike meld) => meld switch {
      Kan k => k.KanSource == TileSource.Ankan ? "ankan"
          : k.KanSource == TileSource.Kakan ? "added-kan" : "daiminkan",
      Kou => "pon",
      Shun => "chii",
      Jantou => "pair",
      _ => "meld",
    };

    private string SourceLabel(int seat, PublicGameView view) {
      if (seat == view.Seat) {
        return language switch {
          "zhs" or "zht" => "你",
          "ja" => "あなた",
          _ => "you",
        };
      }
      return labeler.Label(seat);
    }

    private static int SelfShanten(PublicGameView view) {
      var hand = new List<GameTile>(view.SelfHand.freeTiles);
      if (view.SelfHand.pendingTile != null) {
        hand.Add(view.SelfHand.pendingTile);
      }
      return view.ShantenOf(hand);
    }

    private string DescribeSelfHand(PublicGameView view) {
      var hand = view.SelfHand;
      var sb = new StringBuilder(TileNotation.Group(hand.freeTiles, language));
      if (hand.pendingTile != null) {
        sb.Append(language switch {
          "zhs" or "zht" => " + 摸入 ",
          "ja" => " + ツモ ",
          _ => " + drew ",
        }).Append(TileNotation.One(hand.pendingTile, language));
      }
      if (hand.called.Count > 0) {
        sb.Append(language switch {
          "zhs" or "zht" or "ja" => " | 副露: ",
          _ => " | melds: ",
        }).Append(string.Join(" ; ", hand.called.Select(m => DescribeMeld(m, view))));
      }
      if (hand.nukiDora.Count > 0) {
        sb.Append(language switch {
          "zhs" => $" | 拔北: {hand.nukiDora.Count}x 4z",
          "zht" => $" | 拔北: {hand.nukiDora.Count}x 4z",
          "ja" => $" | 抜きドラ: {hand.nukiDora.Count}x 4z",
          _ => $" | nukidora: {hand.nukiDora.Count}x 4z",
        });
      }
      return sb.ToString();
    }

    private string BuildTurnFooterJsonHint() {
      if (promptConfig.EnableTableTalk) {
        return language switch {
          "zhs" => "仅回复 JSON：{\"action\": <编号>, \"rationale\": \"<简明战术理由>\", \"chat\": \"<可选牌桌发言>\"}",
          "zht" => "僅回覆 JSON：{\"action\": <編號>, \"rationale\": \"<簡明戰術理由>\", \"chat\": \"<可選牌桌發言>\"}",
          "ja" => "JSONのみで応答してください: {\"action\": <ID>, \"rationale\": \"<簡潔な判断理由>\", \"chat\": \"<任意の卓上チャット>\"}",
          _ => "Respond with JSON only: {\"action\": <id>, \"rationale\": \"<why>\", \"chat\": \"<optional table talk>\"}",
        };
      }
      return language switch {
        "zhs" => "仅回复 JSON：{\"action\": <编号>, \"rationale\": \"<简明战术理由>\"}",
        "zht" => "僅回覆 JSON：{\"action\": <編號>, \"rationale\": \"<簡明戰術理由>\"}",
        "ja" => "JSONのみで応答してください: {\"action\": <ID>, \"rationale\": \"<簡潔な判断理由>\"}",
        _ => "Respond with JSON only: {\"action\": <id>, \"rationale\": \"<why>\"}",
      };
    }

    private string BuildOutputContract() {
      if (promptConfig.EnableTableTalk) {
        return language switch {
          "zhs" =>
              "仅回复一个 JSON 对象，不要输出任何额外文字：\n" +
              "{\"action\": <合法动作菜单中的整数编号>, \"rationale\": \"<1-2 句简明扼要的简体中文战术理由>\", \"chat\": \"<可选的简短牌桌发言，对手可见>\"}\n" +
              "\"action\" 字段必须是当前回合合法动作菜单中列出的整数编号。请用简体中文撰写 \"rationale\"（以及可选的 \"chat\"），不要在 JSON 对象外添加任何说明。",
          "zht" =>
              "僅回覆一個 JSON 物件，不要輸出任何額外文字：\n" +
              "{\"action\": <合法動作選單中的整數編號>, \"rationale\": \"<1-2 句簡明扼要的繁體中文戰術理由>\", \"chat\": \"<可選的簡短牌桌發言，對手可見>\"}\n" +
              "\"action\" 欄位必須是當前回合合法動作選單中列出的整數編號。請用繁體中文撰寫 \"rationale\"（以及可選的 \"chat\"），不要在 JSON 物件外添加任何說明。",
          "ja" =>
              "単一の JSON オブジェクトのみを出力し、それ以外の文章は一切含めないでください:\n" +
              "{\"action\": <合法アクション一覧の整数ID>, \"rationale\": \"<1〜2文の簡潔な日本語での戦術的根拠>\", \"chat\": \"<他家にも見える任意の短い卓上チャット>\"}\n" +
              "\"action\" フィールドは必ず現在の手番の合法アクション一覧にある整数IDにしてください。\"rationale\"（および任意の \"chat\"）は日本語で記述し、JSON 外にテキストを出力しないでください。",
          _ =>
              "Respond with a SINGLE JSON object and nothing else:\n" +
              "{\"action\": <integer id from Legal Actions>, \"rationale\": \"<1-2 concise sentences explaining your tactical reasoning>\", \"chat\": \"<optional short table-talk line visible to opponents>\"}\n" +
              "The \"action\" field MUST be an integer id listed in the current turn's Legal Actions menu. Do not wrap output in prose outside the JSON object.",
        };
      }
      return language switch {
        "zhs" =>
            "仅回复一个 JSON 对象，不要输出任何额外文字：\n" +
            "{\"action\": <合法动作菜单中的整数编号>, \"rationale\": \"<1-2 句简明扼要的简体中文战术理由>\"}\n" +
            "\"action\" 字段必须是当前回合合法动作菜单中列出的整数编号。请用简体中文撰写 \"rationale\"，不要在 JSON 对象外添加任何说明。",
        "zht" =>
            "僅回覆一個 JSON 物件，不要輸出任何額外文字：\n" +
            "{\"action\": <合法動作選單中的整數編號>, \"rationale\": \"<1-2 句簡明扼要的繁體中文戰術理由>\"}\n" +
            "\"action\" 欄位必須是當前回合合法動作選單中列出的整數編號。請用繁體中文撰寫 \"rationale\"，不要在 JSON 物件外添加任何說明。",
        "ja" =>
            "単一の JSON オブジェクトのみを出力し、それ以外の文章は一切含めないでください:\n" +
            "{\"action\": <合法アクション一覧の整数ID>, \"rationale\": \"<1〜2文の簡潔な日本語での戦術的根拠>\"}\n" +
            "\"action\" フィールドは必ず現在の手番の合法アクション一覧にある整数IDにしてください。\"rationale\" は日本語で記述し、JSON 外にテキストを出力しないでください。",
        _ =>
            "Respond with a SINGLE JSON object and nothing else:\n" +
            "{\"action\": <integer id from Legal Actions>, \"rationale\": \"<1-2 concise sentences explaining your tactical reasoning>\"}\n" +
            "The \"action\" field MUST be an integer id listed in the current turn's Legal Actions menu. Do not wrap output in prose outside the JSON object.",
      };
    }

    private static string TileNotationHint(string lang) => lang switch {
      "zhs" =>
          "花色：`m` = 万子（1m-9m），`p` = 筒子（1p-9p），`s` = 索子（1s-9s），`z` = 字牌。" +
          "字牌：`1z`=东风，`2z`=南风，`3z`=西风，`4z`=北风，`5z`=白，`6z`=发，`7z`=中。" +
          "数字 `0` 表示红五宝牌（`0m`、`0p`、`0s` = 红5万/红5筒/红5索，和牌各 +1 番）。" +
          "符号 `*` 表示万能牌（绝非立直标记；立直宣言牌标记为 `[riichi]`）。",
      "zht" =>
          "花色：`m` = 萬子（1m-9m），`p` = 筒子（1p-9p），`s` = 索子（1s-9s），`z` = 字牌。" +
          "字牌：`1z`=東風，`2z`=南風，`3z`=西風，`4z`=北風，`5z`=白，`6z`=發，`7z`=中。" +
          "數字 `0` 表示紅五寶牌（`0m`、`0p`、`0s` = 紅5萬/紅5筒/紅5索，和牌各 +1 飜）。" +
          "符號 `*` 表示萬能牌（絕非立直標記；立直宣言牌標記為 `[riichi]`）。",
      "ja" =>
          "スート: `m` = 萬子（1m-9m）、`p` = 筒子（1p-9p）、`s` = 索子（1s-9s）、`z` = 字牌。" +
          "字牌: `1z`=東、`2z`=南、`3z`=西、`4z`=北、`5z`=白、`6z`=發、`7z`=中。" +
          "数字 `0` は赤五ドラ（`0m`・`0p`・`0s` = 赤5萬/赤5筒/赤5索、各+1飜）を表します。" +
          "記号 `*` はオールマイティ万能牌を表します（リーチ記号ではありません。リーチ宣言牌は `[riichi]` と表記されます）。",
      _ =>
          "Suits: `m` = Manzu (characters 1m-9m), `p` = Pinzu (dots 1p-9p), `s` = Souzu (bamboo 1s-9s), `z` = Honors. " +
          "Honors: `1z`=East wind, `2z`=South wind, `3z`=West wind, `4z`=North wind, `5z`=White dragon (Haku), `6z`=Green dragon (Hatsu), `7z`=Red dragon (Chun). " +
          "Digit `0` denotes a red five (`0m`, `0p`, `0s` = red 5m/5p/5s, worth +1 akadora han). " +
          "Symbol `*` denotes a universal WILDCARD tile (never a riichi marker; riichi discards are marked `[riichi]`).",
    };

    private static string LoadTemplate(string baseName, string language) {
      var lang = NormalizeArenaLanguage(language);
      var assembly = typeof(ArenaPromptBuilder).Assembly;
      string resourceName = null;

      if (lang != "en") {
        var localized = $".Prompts.{baseName}_{lang}.md";
        resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(localized, StringComparison.Ordinal));
      }
      resourceName ??= assembly.GetManifestResourceNames()
          .SingleOrDefault(name => name.EndsWith($".Prompts.{baseName}.md", StringComparison.Ordinal));

      if (resourceName == null) {
        throw new InvalidOperationException($"Arena prompt template not found: {baseName} ({language}).");
      }

      using var stream = assembly.GetManifestResourceStream(resourceName);
      using var reader = new StreamReader(stream!);
      return reader.ReadToEnd();
    }

    private static Tile[] BuildStandardKinds() {
      var list = new List<Tile>(34);
      foreach (var suit in new[] { TileSuit.M, TileSuit.P, TileSuit.S }) {
        for (int n = 1; n <= 9; n++) list.Add(new Tile(suit, n));
      }
      for (int z = 1; z <= 7; z++) list.Add(new Tile(TileSuit.Z, z));
      return list.ToArray();
    }

    private static char SuitLetter(TileSuit s) => s switch {
      TileSuit.M => 'm',
      TileSuit.P => 'p',
      TileSuit.S => 's',
      TileSuit.Z => 'z',
      _ => '?',
    };

    private static string ShortKindCode(Tile t) => $"{t.Num}{SuitLetter(t.Suit)}";

    private static bool HasStandardYakumanCap(string scoringOptions) {
      if (string.IsNullOrWhiteSpace(scoringOptions)) return false;
      foreach (var raw in scoringOptions.Split(',')) {
        var token = raw.Trim();
        if (string.Equals(token, "Yakuman", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "Default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "All", StringComparison.OrdinalIgnoreCase)) {
          return true;
        }
      }
      return false;
    }

    private static string WindLabel(Wind wind, string lang) => (wind, lang) switch {
      (Wind.E, "zhs") => "东",
      (Wind.S, "zhs") => "南",
      (Wind.W, "zhs") => "西",
      (Wind.N, "zhs") => "北",
      (Wind.E, "zht" or "ja") => "東",
      (Wind.S, "zht" or "ja") => "南",
      (Wind.W, "zht" or "ja") => "西",
      (Wind.N, "zht" or "ja") => "北",
      (Wind.E, _) => "East",
      (Wind.S, _) => "South",
      (Wind.W, _) => "West",
      (Wind.N, _) => "North",
      _ => wind.ToString(),
    };
  }

  public sealed record ArenaChatLine(string Sender, string Text);
}
