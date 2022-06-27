using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/ws")]
    public class WebSocketController : ControllerBase {
        private readonly ILogger<WebSocketController> logger;

        public WebSocketController(ILogger<WebSocketController> logger) {
            this.logger = logger;
        }

        [HttpGet("{id}")]
        public async Task ConnectWS([FromRoute(Name = "id"), RequireAuth] User user) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                using var webSocket =
                    await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
                        DangerousEnableCompression = true
                    });

                var rabiCtx = user.Connect(webSocket);
                if (rabiCtx == null) {
                    return;
                }
                if (!await rabiCtx.HandShake()) {
                    return;
                }
                var room = user.room;
                if (room != null) {
                    room.BroadcastRoomState();
                }
                try {
                    await Task.Delay(TimeSpan.FromDays(7), rabiCtx.cts.Token);
                } catch (OperationCanceledException) { }
            } else {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}