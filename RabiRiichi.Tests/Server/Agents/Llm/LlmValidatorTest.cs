using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  /// <summary> A provider stub returning a scripted result. </summary>
  internal sealed class StubProvider(LlmResult result) : ILlmProvider {
    public LlmProvider Provider => LlmProvider.Openai;
    public Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens, CancellationToken ct) {
      return Task.FromResult(result);
    }
  }

  internal sealed class StubFactory(LlmResult result) : ILlmProviderFactory {
    public ILlmProvider Create(LlmSettings settings) => new StubProvider(result);
  }

  [TestClass]
  public class LlmValidatorTest {
    private static LlmSettings Settings() => LlmSettings.FromProto(new LlmAiConfig {
      Provider = LlmProvider.Openai,
      ApiToken = "sk-test",
      Model = "gpt-4o-mini",
      Language = "en",
    }, out _);

    [TestMethod]
    public async Task Validate_SucceedsOnOkResponse() {
      var validator = new LlmValidator(new StubFactory(LlmResult.Ok("OK")));
      var result = await validator.ValidateAsync(Settings());
      Assert.IsTrue(result.Ok);
    }

    [TestMethod]
    public async Task Validate_FailsAndClassifiesAuth() {
      var validator = new LlmValidator(new StubFactory(LlmResult.Fail("HTTP 401: bad key")));
      var result = await validator.ValidateAsync(Settings());
      Assert.IsFalse(result.Ok);
      Assert.AreEqual("auth", result.Reason);
    }

    [TestMethod]
    public async Task Validate_FailsAndClassifiesModel() {
      var validator = new LlmValidator(new StubFactory(LlmResult.Fail("HTTP 404: no such model")));
      var result = await validator.ValidateAsync(Settings());
      Assert.AreEqual("model", result.Reason);
    }

    [TestMethod]
    public async Task Validate_FailsAndClassifiesTimeout() {
      var validator = new LlmValidator(new StubFactory(LlmResult.Fail("timeout")));
      var result = await validator.ValidateAsync(Settings());
      Assert.AreEqual("timeout", result.Reason);
    }

    [TestMethod]
    public void ClassifyError_MapsCommonCases() {
      Assert.AreEqual("timeout", LlmValidator.ClassifyError("Request timeout"));
      Assert.AreEqual("auth", LlmValidator.ClassifyError("403 forbidden"));
      Assert.AreEqual("auth", LlmValidator.ClassifyError("invalid api key"));
      Assert.AreEqual("model", LlmValidator.ClassifyError("model not found"));
      Assert.AreEqual("rate_limit", LlmValidator.ClassifyError("HTTP 429"));
      Assert.AreEqual("unreachable", LlmValidator.ClassifyError("connection refused"));
      Assert.AreEqual("unknown", LlmValidator.ClassifyError(""));
    }
  }
}
