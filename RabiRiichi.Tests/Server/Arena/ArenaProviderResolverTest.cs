using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Eval;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents.Llm;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Arena {
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
    public void ParseThinkingLevel_DefaultsToMinimalForEmptyOrUnknown() {
      Assert.AreEqual(LlmThinkingLevel.Minimal, ArenaProviderResolver.ParseThinkingLevel(""));
      Assert.AreEqual(LlmThinkingLevel.Minimal, ArenaProviderResolver.ParseThinkingLevel(null));
      Assert.AreEqual(LlmThinkingLevel.Minimal, ArenaProviderResolver.ParseThinkingLevel("bogus"));
    }

    [TestMethod]
    public void AnthropicBuildRequestBody_ExtractsSystemMergesRolesAndConfiguresThinking() {
      var messages = new List<LlmMessage> {
        LlmMessage.System("System part 1"),
        LlmMessage.System("System part 2"),
        LlmMessage.User("Turn 1 state"),
        LlmMessage.User("Retry warning"),
        LlmMessage.Assistant("{\"action\":0,\"rationale\":\"ok\"}"),
        LlmMessage.User("Turn 2 state"),
      };

      var body = AnthropicProvider.BuildRequestBody(
          messages, maxOutputTokens: 512, model: "claude-sonnet-4-5",
          thinkingLevel: LlmThinkingLevel.Medium, temperature: 0.3);

      Assert.AreEqual("claude-sonnet-4-5", body["model"]!.GetValue<string>());
      StringAssert.Contains(body["system"]!.GetValue<string>(), "System part 1");
      StringAssert.Contains(body["system"]!.GetValue<string>(), "System part 2");

      // Consecutive user messages are coalesced into a single user turn.
      var arr = body["messages"]!.AsArray();
      Assert.AreEqual(3, arr.Count);
      Assert.AreEqual("user", arr[0]!["role"]!.GetValue<string>());
      StringAssert.Contains(arr[0]!["content"]!.GetValue<string>(), "Turn 1 state");
      StringAssert.Contains(arr[0]!["content"]!.GetValue<string>(), "Retry warning");
      Assert.AreEqual("assistant", arr[1]!["role"]!.GetValue<string>());
      Assert.AreEqual("user", arr[2]!["role"]!.GetValue<string>());

      // Medium thinking sets budget_tokens=2048, bumps max_tokens > budget, and omits temperature.
      Assert.IsNotNull(body["thinking"]);
      Assert.AreEqual(2048, body["thinking"]!["budget_tokens"]!.GetValue<int>());
      Assert.IsTrue(body["max_tokens"]!.GetValue<int>() > 2048);
      Assert.IsNull(body["temperature"]);
    }

    [TestMethod]
    public void AnthropicParseResponse_ExtractsTextThinkingAndTokens() {
      const string raw = """
      {
        "content": [
          { "type": "thinking", "thinking": "Evaluating shanten and safety..." },
          { "type": "text", "text": "{\"action\": 1, \"rationale\": \"discard isolated wind\"}" }
        ],
        "usage": {
          "input_tokens": 300,
          "cache_read_input_tokens": 120,
          "output_tokens": 55
        }
      }
      """;

      var res = AnthropicProvider.ParseResponse(raw);
      Assert.IsTrue(res.Success);
      StringAssert.Contains(res.Content, "\"action\": 1");
      Assert.AreEqual("Evaluating shanten and safety...", res.ReasoningContent);
      Assert.AreEqual(420, res.PromptTokens);
      Assert.AreEqual(55, res.CompletionTokens);
    }

    [TestMethod]
    public void Resolve_SupportsAnthropicGeminiOpenAiGrokAndLocalKeylessBaseUrl() {
      using var http = new HttpClient(new StubHttpHandler("{}"));

      var anthropic = ArenaProviderResolver.Resolve(new ArenaConfig.ModelConfig {
        Id = "c1", Provider = "anthropic", Model = "claude-sonnet-4-5", ApiKey = "sk-ant",
      }, http);
      Assert.IsInstanceOfType(anthropic, typeof(AnthropicProvider));

      var gemini = ArenaProviderResolver.Resolve(new ArenaConfig.ModelConfig {
        Id = "g1", Provider = "gemini", Model = "gemini-2.5-flash", ApiKey = "gem-key",
      }, http);
      Assert.IsInstanceOfType(gemini, typeof(GeminiProvider));

      var localOpenAi = ArenaProviderResolver.Resolve(new ArenaConfig.ModelConfig {
        Id = "local1", Provider = "openai", Model = "qwen-local",
        BaseUrl = "http://127.0.0.1:11434", ApiKey = "",
      }, http);
      Assert.IsInstanceOfType(localOpenAi, typeof(OpenAiProvider));

      var grok = ArenaProviderResolver.Resolve(new ArenaConfig.ModelConfig {
        Id = "x1", Provider = "grok", Model = "grok-3", ApiKey = "xai-key",
      }, http);
      Assert.IsInstanceOfType(grok, typeof(GrokProvider));
    }

    [TestMethod]
    public async Task TestConnectionAsync_BaselineSucceedsAndMockedLlmValidatesJsonChoice() {
      var baselineOutcome = await ArenaProviderResolver.TestConnectionAsync(
          new ArenaConfig.ModelConfig {
            Id = "base", Provider = "baseline", Variant = "balanced",
          });
      Assert.IsTrue(baselineOutcome.Ok);
      Assert.AreEqual(0, baselineOutcome.ParsedAction);

      const string openAiOk = """
      {
        "choices": [
          { "message": { "content": "{\"action\": 1, \"rationale\": \"cut guest East wind\"}" } }
        ],
        "usage": { "prompt_tokens": 90, "completion_tokens": 18 }
      }
      """;
      using var okHttp = new HttpClient(new StubHttpHandler(openAiOk));
      var okOutcome = await ArenaProviderResolver.TestConnectionAsync(
          new ArenaConfig.ModelConfig {
            Id = "m1", Provider = "openai", Model = "gpt-test",
            BaseUrl = "http://127.0.0.1:9999",
          },
          timeoutSeconds: 10,
          http: okHttp);
      Assert.IsTrue(okOutcome.Ok, okOutcome.Error);
      Assert.AreEqual(1, okOutcome.ParsedAction);
      Assert.AreEqual("cut guest East wind", okOutcome.Rationale);
      Assert.AreEqual(90, okOutcome.PromptTokens);

      const string openAiOutOfRange = """
      {
        "choices": [
          { "message": { "content": "{\"action\": 99, \"rationale\": \"invalid choice\"}" } }
        ]
      }
      """;
      using var badHttp = new HttpClient(new StubHttpHandler(openAiOutOfRange));
      var badOutcome = await ArenaProviderResolver.TestConnectionAsync(
          new ArenaConfig.ModelConfig {
            Id = "m2", Provider = "openai", Model = "gpt-test",
            BaseUrl = "http://127.0.0.1:9999",
          },
          timeoutSeconds: 10,
          http: badHttp);
      Assert.IsFalse(badOutcome.Ok);
      StringAssert.Contains(badOutcome.Error, "not 0 or 1");
    }

    private sealed class StubHttpHandler(string json, HttpStatusCode code = HttpStatusCode.OK)
        : HttpMessageHandler {
      protected override Task<HttpResponseMessage> SendAsync(
          HttpRequestMessage request, CancellationToken cancellationToken) {
        return Task.FromResult(new HttpResponseMessage(code) {
          Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
      }
    }
  }
}
