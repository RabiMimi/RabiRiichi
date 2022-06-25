using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : ControllerBase {
        private readonly ILogger<RoomController> logger;

        public InfoController(ILogger<RoomController> logger) {
            this.logger = logger;
        }

        [HttpGet("")]
        public ActionResult<ServerInfo> GetInfo() {
            return Ok(new ServerInfo());
        }
    }
}