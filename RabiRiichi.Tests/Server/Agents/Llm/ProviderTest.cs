using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class ProviderTest {
    private static LlmSettings OpenAi(string baseUrl = null) => LlmSettings.FromProto(
        new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk-test",
          Model = "gpt-4o-mini",
          Language = "en",
          BaseUrl = baseUrl ?? "",
        }, out _);

    private static LlmSettings Gemini() => LlmSettings.FromProto(
        new LlmAiConfig {
          Provider = LlmProvider.Gemini,
          ApiToken = "key-123",
          Model = "gemini-2.0-flash",
          Language = "en",
        }, out _);

    private static readonly IReadOnlyList<LlmMessage> Messages = new List<LlmMessage> {
      LlmMessage.System("sys"),
      LlmMessage.User("hello"),
    };

    // ---- OpenAI ----

    [TestMethod]
    public async Task OpenAi_ParsesContentAndSetsAuthAndUrl() {
      var body = "{\"choices\":[{\"message\":{\"content\":\"hi there\"}}]}";
      var handler = new FakeHttpHandler(HttpStatusCode.OK, body);
      var provider = new OpenAiProvider(handler.Client(), OpenAi());

      var result = await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      Assert.IsTrue(result.Success);
      Assert.AreEqual("hi there", result.Content);
      Assert.AreEqual("https://api.openai.com/v1/chat/completions",
          handler.LastRequest.RequestUri.ToString());
      Assert.AreEqual("Bearer sk-test",
          handler.LastRequest.Headers.GetValues("Authorization").First());
      var sent = JsonNode.Parse(handler.LastRequestBody);
      Assert.AreEqual("gpt-4o-mini", sent["model"].GetValue<string>());
      Assert.AreEqual(2, sent["messages"].AsArray().Count);
      Assert.AreEqual("system", sent["messages"][0]["role"].GetValue<string>());
    }

    [TestMethod]
    public async Task OpenAi_UsesCustomBaseUrl() {
      var handler = new FakeHttpHandler(HttpStatusCode.OK,
          "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
      var provider = new OpenAiProvider(handler.Client(), OpenAi("https://proxy.local"));
      await provider.CompleteAsync(Messages, 100, CancellationToken.None);
      Assert.AreEqual("https://proxy.local/v1/chat/completions",
          handler.LastRequest.RequestUri.ToString());
    }

    [TestMethod]
    public async Task OpenAi_HttpErrorReportsFailure() {
      var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}");
      var provider = new OpenAiProvider(handler.Client(), OpenAi());
      var result = await provider.CompleteAsync(Messages, 100, CancellationToken.None);
      Assert.IsFalse(result.Success);
      StringAssert.Contains(result.Error, "401");
    }

    [TestMethod]
    public void OpenAi_ParseContent_EmptyIsFailure() {
      Assert.IsFalse(OpenAiProvider.ParseContent("{\"choices\":[]}").Success);
      Assert.IsFalse(OpenAiProvider.ParseContent("garbage").Success);
    }

    // ---- Gemini ----

    [TestMethod]
    public async Task Gemini_ParsesContentAndMapsRolesAndKey() {
      var body = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"pon!\"}]}}]}";
      var handler = new FakeHttpHandler(HttpStatusCode.OK, body);
      var provider = new GeminiProvider(handler.Client(), Gemini());

      var result = await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      Assert.IsTrue(result.Success);
      Assert.AreEqual("pon!", result.Content);
      var url = handler.LastRequest.RequestUri.ToString();
      StringAssert.Contains(url, "/v1beta/models/gemini-2.0-flash:generateContent");
      StringAssert.Contains(url, "key=key-123");
    }

    [TestMethod]
    public void Gemini_BuildBody_MapsSystemInstructionAndModelRole() {
      var body = GeminiProvider.BuildBody(new List<LlmMessage> {
        LlmMessage.System("be nice"),
        LlmMessage.User("u1"),
        LlmMessage.Assistant("a1"),
        LlmMessage.User("u2"),
      }, 128);

      // System instruction is separated out.
      Assert.AreEqual("be nice",
          body["system_instruction"]["parts"][0]["text"].GetValue<string>());
      var contents = body["contents"].AsArray();
      Assert.AreEqual(3, contents.Count);
      Assert.AreEqual("user", contents[0]["role"].GetValue<string>());
      Assert.AreEqual("model", contents[1]["role"].GetValue<string>());
      Assert.AreEqual(128,
          body["generationConfig"]["maxOutputTokens"].GetValue<int>());
    }

    [TestMethod]
    public void Gemini_ParseContent_EmptyIsFailure() {
      Assert.IsFalse(GeminiProvider.ParseContent("{\"candidates\":[]}").Success);
    }
  }
}
