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
  }
}
