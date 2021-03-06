using Grpc.Core;
using RabiRiichi.Server.Models;
using System.Security.Claims;

namespace RabiRiichi.Server.Auth {
    public static class AuthExtensions {
        public static User Fetch(this UserList userList, HttpContext context) {
            if (!userList.TryFetch(context, out var user)) {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "User does not exist"));
            }
            return user;
        }

        public static User Fetch(this UserList userList, ServerCallContext context) {
            return userList.Fetch(context.GetHttpContext());
        }

        public static bool TryFetch(this UserList userList, HttpContext context, out User user) {
            try {
                var uidStr = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(uidStr)) {
                    user = null;
                    return false;
                }
                int uid = Convert.ToInt32(uidStr, 16);
                return userList.TryGet(uid, out user);
            } catch {
                user = null;
                return false;
            }
        }

        public static bool TryFetch(this UserList userList, ServerCallContext context, out User user) {
            return userList.TryFetch(context.GetHttpContext(), out user);
        }
    }
}