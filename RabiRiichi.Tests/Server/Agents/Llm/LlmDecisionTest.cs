using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmDecisionTest {
    [TestMethod]
    public void Parse_ReadsChatOnlySchema() {
      var result = LlmDecision.Parse("{\"say\":\"hi\",\"sticker\":\"happy\"}");
      Assert.AreEqual("hi", result.Say);
      Assert.AreEqual("happy", result.Sticker);
    }

    [TestMethod]
    public void Parse_AcceptsExplicitNulls() {
      var result = LlmDecision.Parse("{\"say\":null,\"sticker\":null}");
      Assert.IsNull(result.Say);
      Assert.IsNull(result.Sticker);
    }

    [TestMethod]
    public void Parse_ToleratesFenceAndProse() {
      var result = LlmDecision.Parse("Result:\n```json\n{\"say\":\"pon!\",\"sticker\":null}\n```");
      Assert.AreEqual("pon!", result.Say);
    }

    [TestMethod]
    public void Parse_IgnoresLegacyChoiceOnlyObject() {
      var result = LlmDecision.Parse("{\"choice\":3}");
      Assert.IsNull(result.Say);
      Assert.IsNull(result.Sticker);
    }

    [TestMethod]
    public void Parse_MalformedResponseIsEmpty() {
      var result = LlmDecision.Parse("not json");
      Assert.IsNull(result.Say);
      Assert.IsNull(result.Sticker);
    }

    [TestMethod]
    public void Parse_ReadsTheChosenActionId() {
      var result = LlmDecision.Parse("{\"action\":4,\"say\":\"riichi!\",\"sticker\":null}");
      Assert.IsTrue(result.HasAction);
      Assert.AreEqual(4, result.Action);
      Assert.AreEqual("riichi!", result.Say);
    }

    [TestMethod]
    public void Parse_AcceptsAnActionIdSentAsAString() {
      // Models routinely quote numbers; a quoted id must not cost us the turn.
      var result = LlmDecision.Parse("{\"action\":\"7\",\"say\":null}");
      Assert.AreEqual(7, result.Action);
    }

    [TestMethod]
    public void Parse_AcceptsAnActionOnlyResponse() {
      var result = LlmDecision.Parse("```json\n{\"action\": 0}\n```");
      Assert.AreEqual(0, result.Action);
      Assert.IsNull(result.Say);
    }

    [TestMethod]
    public void Parse_ChatOnlyResponseHasNoAction() {
      // The end-of-game prompt still uses the chat-only schema.
      var result = LlmDecision.Parse("{\"say\":\"gg\",\"sticker\":\"smile\"}");
      Assert.IsFalse(result.HasAction);
      Assert.IsNull(result.Action);
    }

    [TestMethod]
    public void Parse_NonNumericActionIsDropped() {
      var result = LlmDecision.Parse("{\"action\":\"discard 1m\",\"say\":null}");
      Assert.IsFalse(result.HasAction);
    }
  }
}
