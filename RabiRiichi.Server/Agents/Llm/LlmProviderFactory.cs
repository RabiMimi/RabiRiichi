using System;
using System.Net.Http;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary> Creates an <see cref="ILlmProvider"/> for a given config. </summary>
  public interface ILlmProviderFactory {
    ILlmProvider Create(LlmSettings settings);
  }

  /// <summary>
  /// Default factory backed by an <see cref="IHttpClientFactory"/>. Each provider
  /// gets a fresh <see cref="HttpClient"/> with the per-request timeout applied
  /// via the cancellation token (not HttpClient.Timeout, so we can distinguish
  /// timeout from other errors cleanly).
  /// </summary>
  public sealed class LlmProviderFactory(IHttpClientFactory httpFactory) : ILlmProviderFactory {
    private readonly IHttpClientFactory httpFactory = httpFactory;

    public ILlmProvider Create(LlmSettings settings) {
      var http = httpFactory.CreateClient("llm");
      return settings.Provider switch {
        ProtoLlmProvider.Openai => new OpenAiProvider(http, settings),
        ProtoLlmProvider.Gemini => new GeminiProvider(http, settings),
        _ => throw new ArgumentOutOfRangeException(nameof(settings),
            $"Unsupported LLM provider: {settings.Provider}"),
      };
    }
  }
}
