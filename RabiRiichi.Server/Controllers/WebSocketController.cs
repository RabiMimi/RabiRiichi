using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;
using System.Net.Sockets;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/ws")]
    public class WebSocketController : ControllerBase {
        private readonly ILogger<RoomController> logger;
        private static readonly WebSocketAcceptContext socketOption = new() {
            SubProtocol = "json",
            DangerousEnableCompression = true
        };

        public WebSocketController(ILogger<RoomController> logger) {
            this.logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> ConnectWS([FromRoute(Name = "id"), RequireAuth] User user) {
            if (HttpContext.WebSockets.IsWebSocketRequest) {
                using var webSocket =
                    await HttpContext.WebSockets.AcceptWebSocketAsync(socketOption);
                if (!user.Connect(webSocket)) {
                    return BadRequest();
                }
                return Ok();
            } else {
                return BadRequest();
            }
        }
    }
}