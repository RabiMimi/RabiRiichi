using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;

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
                if (user.Connect(webSocket) == null) {
                    return BadRequest();
                }
                // TODO: Wait for socket connection to close
                return Ok();
            } else {
                return BadRequest();
            }
        }
    }
}