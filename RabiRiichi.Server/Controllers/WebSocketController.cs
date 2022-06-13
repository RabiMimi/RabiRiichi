using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/ws")]
    public class WebSocketController : ControllerBase {
        private readonly ILogger<RoomController> logger;

        public WebSocketController(ILogger<RoomController> logger) {
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
                rabiCtx.cts.Token.WaitHandle.WaitOne();
                return;
            } else {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}