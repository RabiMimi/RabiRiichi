using RabiRiichi.Generated.Core;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>Localized, source-explicit kan descriptions for LLM prompts.</summary>
  public static class LlmKanNotation {
    public static string Kind(TileSource source) => source switch {
      TileSource.Ankan => "ankan",
      TileSource.Kakan => "kakan",
      TileSource.Daiminkan => "daiminkan",
      _ => "kan",
    };

    public static string Describe(TileSource source, string language) {
      var explanation = AiLocalization.NormalizeLanguage(language) switch {
        AiLocalization.LangJa => JapaneseExplanation(source),
        AiLocalization.LangZhs => ChineseExplanation(source),
        _ => EnglishExplanation(source),
      };
      return $"{Kind(source).ToUpperInvariant()} ({explanation})";
    }

    private static string EnglishExplanation(TileSource source) => source switch {
      TileSource.Ankan =>
          "closed kan: four matching tiles entirely from your concealed hand; no discard claimed",
      TileSource.Kakan =>
          "added kan: add the fourth matching tile from your hand or draw to your existing open pon",
      TileSource.Daiminkan =>
          "open discard kan: claim another player's discard with three matching tiles from your hand",
      _ => "kan of unknown source",
    };

    private static string JapaneseExplanation(TileSource source) => source switch {
      TileSource.Ankan =>
          "暗槓：他家の捨て牌を使わず、自分の手牌だけで同じ牌4枚を揃える",
      TileSource.Kakan =>
          "加槓：すでにポンした明刻へ、自分の手牌またはツモ牌から同じ4枚目を加える",
      TileSource.Daiminkan =>
          "大明槓：自分の手牌の同じ牌3枚に、他家の捨て牌1枚を加えて鳴く",
      _ => "由来不明の槓",
    };

    private static string ChineseExplanation(TileSource source) => source switch {
      TileSource.Ankan =>
          "暗杠：不使用他家的弃牌，只用自己隐藏手牌中的四张相同牌",
      TileSource.Kakan =>
          "加杠：在自己已经碰出的明刻上，用手牌或摸牌补上第四张相同牌",
      TileSource.Daiminkan =>
          "大明杠：用自己手里的三张相同牌，鸣取另一名玩家打出的第四张牌",
      _ => "来源不明的杠",
    };
  }
}
