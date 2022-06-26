using Grpc.Core;
using Grpc.Core.Interceptors;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Auth {
    public class JwtInterceptor : Interceptor {
        private readonly ILogger<JwtInterceptor> logger;
        private readonly UserList userList;
        private readonly TokenService tokenService;

        public JwtInterceptor(ILogger<JwtInterceptor> logger, UserList userList, TokenService tokenService) {
            this.logger = logger;
            this.userList = userList;
            this.tokenService = tokenService;
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation) {
            var bearer = context.RequestHeaders.GetValue("Authorization");
            if (bearer == null) {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "No Authorization header"));
            }
            if (!bearer.StartsWith("Bearer ")) {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid Authorization header"));
            }
            var token = bearer["Bearer ".Length..];
            if (!tokenService.IsTokenValid(token, out var userId)) {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));
            }
            var user = userList.Get(userId);
            if (user == null) {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Cannot find user from token"));
            }
            return continuation(request, context);
        }
    }
}