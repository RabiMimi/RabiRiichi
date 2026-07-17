using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmSettingsTest {
    private static LlmAiConfig Valid() => new() {
      Provider = LlmProvider.Openai,
      ApiToken = "sk-test",
      Model = "gpt-4o-mini",
      Language = "en",
    };

    [TestMethod]
    public void FromProto_AcceptsValidConfig() {
      var s = LlmSettings.FromProto(Valid(), out var error);
      Assert.IsNotNull(s);
      Assert.IsNull(error);
      Assert.AreEqual(LlmProvider.Openai, s.Provider);
      Assert.AreEqual("gpt-4o-mini", s.Model);
      Assert.AreEqual("https://api.openai.com", s.BaseUrl);
      Assert.AreEqual("en", s.Language);
    }

    [TestMethod]
    public void FromProto_RejectsMissingToken() {
      var cfg = Valid();
      cfg.ApiToken = "";
      Assert.IsNull(LlmSettings.FromProto(cfg, out var error));
      Assert.AreEqual("token", error);
    }

    [TestMethod]
    public void FromProto_RejectsMissingModel() {
      var cfg = Valid();
      cfg.Model = "";
      Assert.IsNull(LlmSettings.FromProto(cfg, out var error));
      Assert.AreEqual("model", error);
    }

    [TestMethod]
    public void FromProto_RejectsUnspecifiedProvider() {
      var cfg = Valid();
      cfg.Provider = LlmProvider.Unspecified;
      Assert.IsNull(LlmSettings.FromProto(cfg, out var error));
      Assert.AreEqual("provider", error);
    }

    [TestMethod]
    public void FromProto_RejectsBadBaseUrl() {
      var cfg = Valid();
      cfg.BaseUrl = "not-a-url";
      Assert.IsNull(LlmSettings.FromProto(cfg, out var error));
      Assert.AreEqual("url", error);
    }

    [TestMethod]
    public void FromProto_TrimsTrailingSlashOnBaseUrl() {
      var cfg = Valid();
      cfg.BaseUrl = "https://proxy.example.com/";
      var s = LlmSettings.FromProto(cfg, out _);
      Assert.AreEqual("https://proxy.example.com", s.BaseUrl);
    }

    [TestMethod]
    public void FromProto_NoCustomNameLeavesEmptyForClientLocalization() {
      // Server must NOT invent a human-facing name; that is client-side.
      var cfg = new LlmAiConfig {
        Provider = LlmProvider.Gemini,
        ApiToken = "key",
        Model = "gemini-2.0-flash",
        Language = "zh-CN",
      };
      var s = LlmSettings.FromProto(cfg, out _);
      Assert.AreEqual("zhs", s.Language);
      Assert.AreEqual("", s.CustomDisplayName);
      // The broadcast nickname is a client-localized sentinel.
      Assert.AreEqual("@llm:gemini", LlmDisplayName.NicknameFor(s));
    }

    [TestMethod]
    public void FromProto_UsesCustomDisplayNameWhenProvided() {
      var cfg = Valid();
      cfg.DisplayName = "MyBot";
      var s = LlmSettings.FromProto(cfg, out _);
      Assert.AreEqual("MyBot", s.CustomDisplayName);
      Assert.AreEqual("MyBot", LlmDisplayName.NicknameFor(s));
    }

    [TestMethod]
    public void FromProto_NullConfigFailsGracefully() {
      Assert.IsNull(LlmSettings.FromProto(null, out var error));
      Assert.AreEqual("missing", error);
    }
  }
}
