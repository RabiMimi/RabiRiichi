using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class RoomController : ControllerBase {
        private readonly ILogger<RoomController> logger;
        private readonly RoomList roomList;
        private readonly UserList userList;

        public RoomController(ILogger<RoomController> logger, RoomList roomList, UserList userList) {
            this.logger = logger;
            this.roomList = roomList;
            this.userList = userList;
        }

        [HttpPost("create")]
        public ActionResult<CreateRoomResp> CreateRoom([FromBody] CreateRoomReq req) {
            var user = userList.Get(req.sessionCode);
            if (user == null) {
                return Unauthorized();
            }
            var room = new Room();
            if (roomList.Add(room)) {
                return Ok(new CreateRoomResp(room.id));
            }
            return StatusCode(503);
        }
    }
}