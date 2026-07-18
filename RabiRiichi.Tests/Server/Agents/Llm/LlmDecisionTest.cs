using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmDecisionTest {
    [TestMethod]
    public void Parse_PlainJson() {
      var d = LlmDecision.Parse("{\"choice\": 3, \"say\": \"hi\", \"sticker\": \"happy\"}");
      Assert.AreEqual(3, d.Choice);
      Assert.AreEqual("hi", d.Say);
      Assert.AreEqual("happy", d.Sticker);
      Assert.IsTrue(d.HasChoice);
    }

    [TestMethod]
    public void Parse_StripsCodeFences() {
      var raw = "```json\n{\"choice\": 1}\n```";
      var d = LlmDecision.Parse(raw);
      Assert.AreEqual(1, d.Choice);
    }

    [TestMethod]
    public void Parse_IgnoresSurroundingProse() {
      var raw = "Sure! Here's my move:\n{\"choice\": 2, \"reason\": \"safe\"}\nHope that helps.";
      var d = LlmDecision.Parse(raw);
      Assert.AreEqual(2, d.Choice);
    }

    [TestMethod]
    public void Parse_AcceptsNumericStringChoice() {
      var d = LlmDecision.Parse("{\"choice\": \"5\"}");
      Assert.AreEqual(5, d.Choice);
    }

    [TestMethod]
    public void Parse_NullSayAndSticker() {
      var d = LlmDecision.Parse("{\"choice\": 0, \"say\": null, \"sticker\": null}");
      Assert.AreEqual(0, d.Choice);
      Assert.IsNull(d.Say);
      Assert.IsNull(d.Sticker);
    }

    [TestMethod]
    public void Parse_MissingChoiceHasNoChoice() {
      var d = LlmDecision.Parse("{\"say\": \"hmm\"}");
      Assert.IsFalse(d.HasChoice);
      Assert.AreEqual(-1, d.Choice);
    }

    [TestMethod]
    public void Parse_GarbageIsSafe() {
      var d = LlmDecision.Parse("not json at all");
      Assert.IsFalse(d.HasChoice);
    }

    [TestMethod]
    public void Parse_EmptyIsSafe() {
      Assert.IsFalse(LlmDecision.Parse("").HasChoice);
      Assert.IsFalse(LlmDecision.Parse(null).HasChoice);
    }

    [TestMethod]
    public void Parse_HandlesNestedObjects() {
      var raw = "prefix {\"choice\": 1, \"obj\": {\"a\": 2}} suffix";
      Assert.AreEqual(1, LlmDecision.Parse(raw).Choice);
    }

    [TestMethod]
    public void Parse_IgnoresBracesInStrings() {
      var raw = "{\"say\": \"} not the end {\", \"choice\": 4}";
      var d = LlmDecision.Parse(raw);
      Assert.AreEqual(4, d.Choice);
      Assert.AreEqual("} not the end {", d.Say);
    }

    [TestMethod]
    public void Parse_SkipsInvalidBraceFragmentBeforeDecision() {
      var raw = "I considered {east or south}, then chose:\n{\"choice\":2}";
      Assert.AreEqual(2, LlmDecision.Parse(raw).Choice);
    }

    [TestMethod]
    public void Parse_SkipsUnrelatedJsonObjectBeforeDecision() {
      var raw = "Debug: {\"confidence\":0.8}\nFinal: {\"choice\":3,\"say\":\"pon\"}";
      var decision = LlmDecision.Parse(raw);
      Assert.AreEqual(3, decision.Choice);
      Assert.AreEqual("pon", decision.Say);
    }

    [TestMethod]
    public void Parse_SkipsMalformedDecisionBeforeValidDecision() {
      var raw = "Draft: {\"choice\":\"later\"}\nFinal: {\"choice\":5}";
      Assert.AreEqual(5, LlmDecision.Parse(raw).Choice);
    }

    [TestMethod]
    public void Parse_FindsDecisionInsideArrayWrapper() {
      var raw = "Result: [{\"choice\":1,\"sticker\":\"happy\"}]";
      var decision = LlmDecision.Parse(raw);
      Assert.AreEqual(1, decision.Choice);
      Assert.AreEqual("happy", decision.Sticker);
    }

    [TestMethod]
    public void Parse_HandlesEscapedQuotesAndBackslashes() {
      var raw = "```json\n{\"choice\":4,\"say\":\"she said \\\"kan\\\" at C:\\\\tiles\"}\n```";
      var decision = LlmDecision.Parse(raw);
      Assert.AreEqual(4, decision.Choice);
      Assert.AreEqual("she said \"kan\" at C:\\tiles", decision.Say);
    }
  }
}
