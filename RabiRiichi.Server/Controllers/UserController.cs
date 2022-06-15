using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Server.Binders;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase {
        private readonly ILogger<RoomController> logger;
        private readonly UserList userList;

        public UserController(ILogger<RoomController> logger, UserList userList) {
            this.logger = logger;
            this.userList = userList;
        }

        [HttpPost("create")]
        public ActionResult<UserInfoResp> CreateUser(
            [FromBody] CreateUserReq req) {
            var user = new User {
                nickname = req.nickname
            };
            if (!userList.Add(user)) {
                return StatusCode(503);
            }
            return Ok(new UserInfoResp(user));
        }

        [HttpGet("info")]
        public ActionResult<UserInfoResp> GetInfo(
            [FromHeader(Name = "Session-Code"), RequireAuth] User user) {
            return Ok(new UserInfoResp(user));
        }
    }
}