using Grpc.Core;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Rpc {
    public class UserServiceImpl : UserService.UserServiceBase {
        private readonly ILogger<UserServiceImpl> logger;
        private readonly UserList userList;

        public UserServiceImpl(ILogger<UserServiceImpl> logger, UserList userList) {
            this.logger = logger;
            this.userList = userList;
        }
    }
}
