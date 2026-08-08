using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  /// <summary>
  /// A test <see cref="HttpMessageHandler"/> that records the last request and
  /// returns a scripted response (or throws) so provider code can be tested with
  /// no network.
  /// </summary>
  public sealed class FakeHttpHandler : HttpMessageHandler {
    public HttpRequestMessage LastRequest { get; private set; }
    public string LastRequestBody { get; private set; }

    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> responder;

    public FakeHttpHandler(HttpStatusCode status, string body) {
      responder = _ => Task.FromResult(new HttpResponseMessage(status) {
        Content = new StringContent(body),
      });
    }

    public FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) {
      this.responder = responder;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
      LastRequest = request;
      if (request.Content != null) {
        LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
      }
      return await responder(request);
    }

    public HttpClient Client() => new(this) {
      Timeout = Timeout.InfiniteTimeSpan,
    };
  }
}
