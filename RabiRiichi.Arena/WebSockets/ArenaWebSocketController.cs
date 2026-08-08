using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.WebSockets;

namespace RabiRiichi.Arena.WebSockets {
  /// <summary>
  /// Hosts Arena's public replay WebSocket at <c>/ws/public</c> — the exact path
  /// the existing web client appends to the configured server base (§12d). It
  /// accepts the socket and defers all protocol work to
  /// <see cref="ArenaPublicWebSocket"/>, mirroring the player server's
  /// <c>WebSocketController.ConnectPublic</c> so the client is a drop-in fit.
  /// </summary>
  [ApiController]
  [Route("/ws")]
  public sealed class ArenaWebSocketController(ArenaPublicWebSocket handler)
      : ControllerBase {
    private readonly ArenaPublicWebSocket handler = handler;

    [HttpGet("public")]
    public async Task ConnectPublic() {
      if (!HttpContext.WebSockets.IsWebSocketRequest) {
        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
      }
      using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
      var adapter = new WebSocketAdapter(webSocket);
      await handler.HandleConnectionAsync(adapter, HttpContext.RequestAborted);
    }
  }
}
