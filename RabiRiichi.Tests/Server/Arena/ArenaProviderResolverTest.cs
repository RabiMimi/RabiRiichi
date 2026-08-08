using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Tests.Server.Arena {
  /// <summary>
  /// Tests for <see cref="ArenaProviderResolver"/> — specifically the mapping of
  /// the config's <c>thinkingLevel</c> string to the server
  /// <see cref="LlmThinkingLevel"/> that is threaded into the providers (§7).
  /// </summary>
  [TestClass]
  public class ArenaProviderResolverTest {
    [TestMethod]
    public void ParseThinkingLevel_MapsKnownValuesCaseInsensitively() {
      Assert.AreEqual(LlmThinkingLevel.Minimal,
          ArenaProviderResolver.ParseThinkingLevel("minimal"));
      Assert.AreEqual(LlmThinkingLevel.Low,
          ArenaProviderResolver.ParseThinkingLevel("LOW"));
      Assert.AreEqual(LlmThinkingLevel.Medium,
          ArenaProviderResolver.ParseThinkingLevel(" Medium "));
      Assert.AreEqual(LlmThinkingLevel.High,
          ArenaProviderResolver.ParseThinkingLevel("high"));
    }

    [TestMethod]
    public void ParseThinkingLevel_DefaultsToHighForEmptyOrUnknown() {
      Assert.AreEqual(LlmThinkingLevel.High, ArenaProviderResolver.ParseThinkingLevel(""));
      Assert.AreEqual(LlmThinkingLevel.High, ArenaProviderResolver.ParseThinkingLevel(null));
      Assert.AreEqual(LlmThinkingLevel.High,
          ArenaProviderResolver.ParseThinkingLevel("bogus"));
    }
  }
}
