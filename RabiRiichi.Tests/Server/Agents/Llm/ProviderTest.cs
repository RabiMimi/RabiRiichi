using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class ProviderTest {
    private static LlmSettings OpenAi(
        string baseUrl = null, string model = "gpt-4o-mini",
        LlmThinkingLevel thinking = LlmThinkingLevel.Minimal) => LlmSettings.FromProto(
        new LlmAiConfig {
          Provider = LlmProvider.Openai,
          ApiToken = "sk-test",
          Model = model,
          Language = "en",
          BaseUrl = baseUrl ?? "",
        }, out _, thinking);

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
    public async Task OpenAi_DisablesThinkingForDeepSeekModels() {
      var handler = new FakeHttpHandler(HttpStatusCode.OK,
          "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
      var provider = new OpenAiProvider(handler.Client(),
          OpenAi("https://api.deepseek.com", "deepseek-v4-flash"));

      await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      var sent = JsonNode.Parse(handler.LastRequestBody);
      Assert.AreEqual("disabled", sent["thinking"]["type"].GetValue<string>());
      Assert.AreEqual("https://api.deepseek.com/v1/chat/completions",
          handler.LastRequest.RequestUri.ToString());
    }

    [TestMethod]
    public async Task OpenAi_DoesNotSendProviderSpecificThinkingToOtherModels() {
      var handler = new FakeHttpHandler(HttpStatusCode.OK,
          "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
      var provider = new OpenAiProvider(handler.Client(), OpenAi());

      await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      // Minimal (default) sends neither `thinking` nor `reasoning_effort`, so
      // plain (non-reasoning) models are unaffected.
      var sent = JsonNode.Parse(handler.LastRequestBody);
      Assert.IsNull(sent["thinking"]);
      Assert.IsNull(sent["reasoning_effort"]);
    }

    [TestMethod]
    public async Task OpenAi_SendsReasoningEffortForHigherThinkingLevels() {
      var handler = new FakeHttpHandler(HttpStatusCode.OK,
          "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
      var provider = new OpenAiProvider(handler.Client(),
          OpenAi(model: "gpt-5", thinking: LlmThinkingLevel.High));

      await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      var sent = JsonNode.Parse(handler.LastRequestBody);
      Assert.AreEqual("high", sent["reasoning_effort"].GetValue<string>());
      Assert.IsNull(sent["thinking"]); // non-DeepSeek uses reasoning_effort only
    }

    [TestMethod]
    public async Task OpenAi_EnablesDeepSeekThinkingForHigherLevels() {
      var handler = new FakeHttpHandler(HttpStatusCode.OK,
          "{\"choices\":[{\"message\":{\"content\":\"x\"}}]}");
      var provider = new OpenAiProvider(handler.Client(),
          OpenAi("https://api.deepseek.com", "deepseek-v4", LlmThinkingLevel.Medium));

      await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      var sent = JsonNode.Parse(handler.LastRequestBody);
      Assert.AreEqual("enabled", sent["thinking"]["type"].GetValue<string>());
      Assert.IsNull(sent["reasoning_effort"]); // DeepSeek uses thinking on/off
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

    // ---- Gemini (Interactions API, raw HTTP) ----

    private static LlmSettings Gemini() => LlmSettings.FromProto(
        new LlmAiConfig {
          Provider = LlmProvider.Gemini,
          ApiToken = "key-123",
          Model = "gemini-3.5-flash",
          Language = "en",
        }, out _);

    private static string InteractionResponse(string id, string text) =>
        "{\"id\":\"" + id + "\",\"status\":\"completed\",\"steps\":[" +
        "{\"type\":\"model_output\",\"content\":[" +
        "{\"type\":\"text\",\"text\":\"" + text + "\"}]}]}";

    [TestMethod]
    public async Task Gemini_PostsToInteractionsWithKeyHeaderAndParsesText() {
      var handler = new FakeHttpHandler(
          HttpStatusCode.OK, InteractionResponse("int-1", "pon!"));
      var provider = new GeminiProvider(handler.Client(), Gemini());

      var result = await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      Assert.IsTrue(result.Success);
      Assert.AreEqual("pon!", result.Content);
      Assert.AreEqual("https://generativelanguage.googleapis.com/v1beta/interactions",
          handler.LastRequest.RequestUri.ToString());
      // Key travels in the header, never the URL.
      Assert.AreEqual("key-123",
          handler.LastRequest.Headers.GetValues("x-goog-api-key").First());
    }

    [TestMethod]
    public void Gemini_BuildBody_FirstTurnSendsSystemAndStepsNoPrevId() {
      var body = GeminiProvider.BuildRequestBody(new List<LlmMessage> {
        LlmMessage.System("be nice"),
        LlmMessage.User("u1"),
        LlmMessage.Assistant("a1"),
        LlmMessage.User("u2"),
      }, 128, previousInteractionId: null, model: "gemini-3.5-flash");

      Assert.AreEqual("gemini-3.5-flash", body["model"].GetValue<string>());
      Assert.AreEqual("be nice", body["system_instruction"].GetValue<string>());
      Assert.IsNull(body["previous_interaction_id"]);
      Assert.AreEqual(128,
          body["generation_config"]["max_output_tokens"].GetValue<int>());
      // Gemini 3 Flash supports minimal, its lowest thinking level.
      Assert.AreEqual("minimal",
          body["generation_config"]["thinking_level"].GetValue<string>());

      // Full non-system history as steps, in order, with correct step types.
      var steps = body["input"].AsArray();
      Assert.AreEqual(3, steps.Count);
      Assert.AreEqual("user_input", steps[0]["type"].GetValue<string>());
      Assert.AreEqual("u1", steps[0]["content"][0]["text"].GetValue<string>());
      Assert.AreEqual("model_output", steps[1]["type"].GetValue<string>());
      Assert.AreEqual("user_input", steps[2]["type"].GetValue<string>());
    }

    [TestMethod]
    public void Gemini_UsesLowestPortableThinkingLevelForProAndUnsupportedFlashModels() {
      var bodyPro = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.1-pro-preview");
      Assert.AreEqual("low",
          bodyPro["generation_config"]["thinking_level"].GetValue<string>());

      var body35FlashLite = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.5-flash-lite");
      Assert.AreEqual("low",
          body35FlashLite["generation_config"]["thinking_level"].GetValue<string>());

      var body36Flash = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.6-flash");
      Assert.AreEqual("low",
          body36Flash["generation_config"]["thinking_level"].GetValue<string>());

      var body35Flash = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.5-flash");
      Assert.AreEqual("minimal",
          body35Flash["generation_config"]["thinking_level"].GetValue<string>());
    }

    [TestMethod]
    public void Gemini_HonorsConfiguredThinkingLevelAboveMinimal() {
      // Minimal maps to the model's lowest ("minimal" for 3.5 flash)...
      var minimal = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.5-flash",
          thinkingLevel: LlmThinkingLevel.Minimal);
      Assert.AreEqual("minimal",
          minimal["generation_config"]["thinking_level"].GetValue<string>());

      // ...while explicit levels pass through as portable values.
      var high = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.5-flash",
          thinkingLevel: LlmThinkingLevel.High);
      Assert.AreEqual("high",
          high["generation_config"]["thinking_level"].GetValue<string>());

      var medium = GeminiProvider.BuildRequestBody(Messages, 128,
          previousInteractionId: null, model: "gemini-3.1-pro-preview",
          thinkingLevel: LlmThinkingLevel.Medium);
      Assert.AreEqual("medium",
          medium["generation_config"]["thinking_level"].GetValue<string>());
    }

    [TestMethod]
    public void Gemini_BuildBody_ContinuationSendsPrevIdAndOnlyLastTurn() {
      var body = GeminiProvider.BuildRequestBody(new List<LlmMessage> {
        LlmMessage.System("be nice"),
        LlmMessage.User("u1"),
        LlmMessage.Assistant("a1"),
        LlmMessage.User("u2"),
      }, 128, previousInteractionId: "int-1", model: "gemini-3.5-flash");

      Assert.AreEqual("int-1", body["previous_interaction_id"].GetValue<string>());
      // system_instruction is interaction-scoped, so it is re-sent every turn.
      Assert.AreEqual("be nice", body["system_instruction"].GetValue<string>());
      // Only the newest user turn is sent as a plain-string input.
      Assert.AreEqual("u2", body["input"].GetValue<string>());
    }

    [TestMethod]
    public async Task Gemini_ChainsPreviousInteractionIdAcrossCalls() {
      var bodies = new List<string>();
      var handler = new FakeHttpHandler(async req => {
        bodies.Add(await req.Content.ReadAsStringAsync());
        var resp = InteractionResponse($"int-{bodies.Count}", "ok");
        return new HttpResponseMessage(HttpStatusCode.OK) {
          Content = new StringContent(resp),
        };
      });
      var provider = new GeminiProvider(handler.Client(), Gemini());

      await provider.CompleteAsync(Messages, 100, CancellationToken.None);
      await provider.CompleteAsync(Messages, 100, CancellationToken.None);

      // First call has no prior id; second call references the first's id.
      var first = JsonNode.Parse(bodies[0]);
      Assert.IsNull(first["previous_interaction_id"]);
      var second = JsonNode.Parse(bodies[1]);
      Assert.AreEqual("int-1", second["previous_interaction_id"].GetValue<string>());
    }

    [TestMethod]
    public async Task Gemini_HttpErrorReportsFailureWithCode() {
      var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized,
          "{\"error\":{\"code\":401,\"message\":\"bad key\",\"status\":\"UNAUTHENTICATED\"}}");
      var provider = new GeminiProvider(handler.Client(), Gemini());
      var result = await provider.CompleteAsync(Messages, 100, CancellationToken.None);
      Assert.IsFalse(result.Success);
      StringAssert.Contains(result.Error, "401");
    }

    [TestMethod]
    public void Gemini_ParseResponse_ReadsIdAndConcatenatesText() {
      var json = "{\"id\":\"int-9\",\"steps\":[" +
          "{\"type\":\"thought\",\"content\":[{\"type\":\"text\",\"text\":\"hmm\"}]}," +
          "{\"type\":\"model_output\",\"content\":[" +
          "{\"type\":\"text\",\"text\":\"pon\"},{\"type\":\"text\",\"text\":\"!\"}]}]}";
      var result = GeminiProvider.ParseResponse(json, out var id);
      Assert.IsTrue(result.Success);
      Assert.AreEqual("pon!", result.Content);
      Assert.AreEqual("int-9", id);
    }

    [TestMethod]
    public void Gemini_ParseResponse_UnusableBodyIsEmptyResponse() {
      // No id and no steps => genuinely unusable body.
      var result = GeminiProvider.ParseResponse("{}", out _);
      Assert.IsFalse(result.Success);
      StringAssert.Contains(result.Error, "empty response");
      Assert.IsFalse(GeminiProvider.ParseResponse("garbage", out _).Success);
    }

    [TestMethod]
    public void Gemini_ParseResponse_ReadsOutputsArrayShape() {
      var json = "{\"id\":\"int-2\",\"outputs\":[{\"text\":\"kan\"}]}";
      var result = GeminiProvider.ParseResponse(json, out var id);
      Assert.IsTrue(result.Success);
      Assert.AreEqual("kan", result.Content);
      Assert.AreEqual("int-2", id);
    }

    [TestMethod]
    public void Gemini_ParseResponse_ReadsOutputTextConvenienceField() {
      var json = "{\"id\":\"int-3\",\"output_text\":\"ron\"}";
      var result = GeminiProvider.ParseResponse(json, out _);
      Assert.IsTrue(result.Success);
      Assert.AreEqual("ron", result.Content);
    }

    [TestMethod]
    public void Gemini_ParseResponse_ValidInteractionNoTextIsReachableButEmpty() {
      // A well-formed interaction (has id) whose model spent its budget on a
      // thought and produced no visible text. This proves reachability, so the
      // error is the distinct "no output text" (not "empty response").
      var json = "{\"id\":\"int-7\",\"steps\":[" +
          "{\"type\":\"thought\",\"content\":[{\"type\":\"text\",\"text\":\"hmm\"}]}]}";
      var result = GeminiProvider.ParseResponse(json, out var id);
      Assert.IsFalse(result.Success);
      Assert.AreEqual("int-7", id);
      StringAssert.Contains(result.Error, "no output text");
    }
  }
}
