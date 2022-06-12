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
        public async Task<ActionResult> ConnectWS([FromRoute(Name = "id"), RequireAuth] User user) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                using var webSocket =
                    await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext {
                        SubProtocol = "json",
                        DangerousEnableCompression = true
                    });

                var rabiCtx = user.Connect(webSocket);
                if (rabiCtx == null) {
                    return BadRequest();
                }
                if (!await rabiCtx.HandShake()) {
                    return BadRequest();
                }
                rabiCtx.cts.Token.WaitHandle.WaitOne();
                return Ok();
            } else {
                return BadRequest();
            }
        }
    }
}