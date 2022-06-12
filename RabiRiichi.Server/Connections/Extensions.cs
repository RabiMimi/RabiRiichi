using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Utils {
    public static class ConnectionExtensions {
        public static async Task<InMessage> WaitResponse(this OutMessage msg,
            TimeSpan? timeout = null) {
            var task = msg.responseTcs.Task;
            if (timeout == null) {
                timeout = ServerConstants.RESPONSE_TIMEOUT;
            }
            try {
                return await task.WaitAsync(timeout.Value);
            } catch (TimeoutException) {
                return null;
            }
        }

        public static async Task<T> WaitResponse<T>(this OutMessage msg,
            TimeSpan? timeout = null) {
            var inMsg = await msg.WaitResponse(timeout);
            if (inMsg == null) {
                return default;
            }
            return inMsg.TryGetMessage<T>(out var ret) ? ret : default;
        }

        public static async Task<bool> HandShake(this RabiWSContext ctx) {
            var msg = ctx.connection.CreateMessage(
                OutMsgType.VersionCheck, new OutVersionCheck());
            ctx.Queue(msg);
            var inMsg = await msg.WaitResponse();
            if (inMsg == null) {
                return false;
            }
            if (!inMsg.TryGetMessage<InVersionCheck>(out var clientVersion)) {
                return false;
            }
            if (!ServerUtils.IsClientVersionSupported(clientVersion.clientVersion)) {
                return false;
            }
            if (!ServerUtils.IsServerVersionSupported(clientVersion.minServerVersion)) {
                return false;
            }
            return true;
        }

        public static void AddRoomListeners(this User user) {
            var onReceive = user.connection.OnReceive;
            onReceive.AddListener((InRoomUpdate msg) => {
                switch (msg.status) {
                    case UserStatus.Ready:
                        user.room?.GetReady(user);
                        break;
                    case UserStatus.InRoom:
                        user.room?.CancelReady(user);
                        break;
                    case UserStatus.None:
                        user.room?.RemovePlayer(user);
                        break;
                    default:
                        break;
                }
            });
        }

        public static void AddListener<T>(this Action<InMessage> action, Action<T> listener) {
            action += (InMessage msg) => {
                if (msg.TryGetMessage<T>(out var msgData)) {
                    listener(msgData);
                }
            };
        }
    }
}