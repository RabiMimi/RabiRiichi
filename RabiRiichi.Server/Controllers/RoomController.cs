using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class RoomController : ControllerBase {
        private readonly ILogger<RoomController> logger;
        private readonly RoomList roomList;

        public RoomController(ILogger<RoomController> logger, RoomList roomList) {
            this.logger = logger;
            this.roomList = roomList;
        }

        [HttpPost("create")]
        public ActionResult<CreateRoomResp> CreateRoom(
            [AuthHeader, RequireAuth] User user) {
            var room = new Room();
            if (roomList.Add(room) && room.AddPlayer(user)) {
                return Ok(new CreateRoomResp(room.id));
            }
            return StatusCode(503);
        }

        [HttpPost("{room}/join")]
        public ActionResult JoinRoom(
            [AuthHeader, RequireAuth] User user,
            [FromRoute, Required] Room room) {
            if (room.AddPlayer(user)) {
                return Ok();
            }
            return StatusCode(503);
        }
    }
}