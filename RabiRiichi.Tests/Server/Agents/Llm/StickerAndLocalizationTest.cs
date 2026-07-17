using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class StickerAndLocalizationTest {
    [TestMethod]
    public void Sticker_ResolvesKnownMood() {
      Assert.AreEqual("mimi/angry.png", StickerRegistry.ResolvePath("angry"));
      Assert.AreEqual("mimi/happy.png", StickerRegistry.ResolvePath("HAPPY"));
    }

    [TestMethod]
    public void Sticker_UnknownMoodIsNull() {
      Assert.IsNull(StickerRegistry.ResolvePath("nonexistent"));
      Assert.IsNull(StickerRegistry.ResolvePath(""));
      Assert.IsNull(StickerRegistry.ResolvePath(null));
    }

    [TestMethod]
    public void Sticker_RegistryMatchesWebClientSet() {
      // Regression guard: the server sticker set MUST equal the web client's
      // mimi sticker files (src/domain/character.ts). Update both together.
      var expectedFiles = new HashSet<string> {
        "angry.png", "awawawa.png", "happy.png",
        "smile.png", "speechless.png", "surprised.png",
      };
      CollectionAssert.AreEquivalent(
          expectedFiles.ToList(), StickerRegistry.Stickers.Keys.ToList());
      Assert.AreEqual("mimi", StickerRegistry.CharacterId);
    }

    [TestMethod]
    public void Sticker_EveryAdvertisedMoodResolvesToAValidPath() {
      // Whatever moods we tell the LLM about must all resolve to real stickers.
      foreach (var mood in StickerRegistry.Moods) {
        var path = StickerRegistry.ResolvePath(mood);
        Assert.IsNotNull(path, $"mood '{mood}' did not resolve");
        StringAssert.StartsWith(path, "mimi/");
        StringAssert.EndsWith(path, ".png");
      }
    }

    [TestMethod]
    public void Language_NormalizesTags() {
      Assert.AreEqual("zhs", AiLocalization.NormalizeLanguage("zh-CN"));
      Assert.AreEqual("zhs", AiLocalization.NormalizeLanguage("zhs"));
      Assert.AreEqual("ja", AiLocalization.NormalizeLanguage("ja-JP"));
      Assert.AreEqual("en", AiLocalization.NormalizeLanguage("en-US"));
      Assert.AreEqual("en", AiLocalization.NormalizeLanguage("fr"));
      Assert.AreEqual("en", AiLocalization.NormalizeLanguage(""));
    }

    [TestMethod]
    public void LlmPromptName_MatchesClientLlmLabels() {
      // Prompt-only name (embedded in the LLM prompt) MUST match the web
      // client's ai.llm.* strings, so a nameless bot knows the name humans see
      // (client localizes the @llm: sentinel to the same label).
      Assert.AreEqual("Gemi狸",
          AiLocalization.LlmPromptName("", LlmProvider.Gemini, "zhs"));
      Assert.AreEqual("Gemibo",
          AiLocalization.LlmPromptName("", LlmProvider.Gemini, "en"));
      Assert.AreEqual("Gemiヌ",
          AiLocalization.LlmPromptName("", LlmProvider.Gemini, "ja"));
      Assert.AreEqual("OpenAI",
          AiLocalization.LlmPromptName("", LlmProvider.Openai, "zhs"));
      Assert.AreEqual("OpenAI",
          AiLocalization.LlmPromptName("", LlmProvider.Openai, "en"));
      Assert.AreEqual("OpenAI",
          AiLocalization.LlmPromptName("", LlmProvider.Openai, "ja"));
    }

    [TestMethod]
    public void LlmPromptName_FallsBackToGenericLlmLabel() {
      // An unknown provider must not leak a raw enum name; it uses the client's
      // generic "LLM" label instead.
      Assert.AreEqual("LLM",
          AiLocalization.LlmPromptName("", (LlmProvider)999, "en"));
      Assert.AreEqual("LLM",
          AiLocalization.LlmPromptName("", (LlmProvider)999, "zhs"));
    }

    [TestMethod]
    public void LlmPromptName_PrefersCustomName() {
      Assert.AreEqual("MyBot",
          AiLocalization.LlmPromptName("MyBot", LlmProvider.Gemini, "zhs"));
    }

    [TestMethod]
    public void LlmSentinelNickname_EncodesProvider() {
      var gemini = LlmSettings.FromProto(new LlmAiConfig {
        Provider = LlmProvider.Gemini, ApiToken = "k", Model = "m", Language = "en",
      }, out _);
      Assert.AreEqual("@llm:gemini", LlmDisplayName.NicknameFor(gemini));

      var openai = LlmSettings.FromProto(new LlmAiConfig {
        Provider = LlmProvider.Openai, ApiToken = "k", Model = "m", Language = "en",
      }, out _);
      Assert.AreEqual("@llm:openai", LlmDisplayName.NicknameFor(openai));
    }

    [TestMethod]
    public void AiDisplayName_LocalizesBuiltInAisMatchingClient() {
      // Must not leak enum names like RULEBASED / DUMMY to the LLM, and MUST
      // match the web client's ai.type.* strings.
      Assert.AreEqual("小和和", AiLocalization.AiDisplayName(AiType.RuleBased, "zhs"));
      Assert.AreEqual("Nodocchi", AiLocalization.AiDisplayName(AiType.RuleBased, "en"));
      Assert.AreEqual("のどっち", AiLocalization.AiDisplayName(AiType.RuleBased, "ja"));
      Assert.AreEqual("口水兔", AiLocalization.AiDisplayName(AiType.Dummy, "zhs"));
      Assert.AreEqual("Drooling Rabbit", AiLocalization.AiDisplayName(AiType.Dummy, "en"));
    }

    [TestMethod]
    public void AiDisplayName_FallsBackToEnglishForUnknownLang() {
      Assert.AreEqual("Nodocchi", AiLocalization.AiDisplayName(AiType.RuleBased, "fr"));
    }
  }
}
