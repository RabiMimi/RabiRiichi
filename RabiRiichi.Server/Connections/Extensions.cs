using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Utils {
    public static class ConnectionExtensions {
        public static async Task<bool> HandShake(this RabiWSContext ctx) {
            var msg = ctx.connection.CreateMessage(
                OutMsgType.VersionCheck, new OutVersionCheck());
            ctx.Queue(msg);
            await msg.WaitResponse.WaitAsync(TimeSpan.FromSeconds(15));
            if (!msg.WaitResponse.IsCompletedSuccessfully) {
                return false;
            }
            if (!msg.WaitResponse.Result.TryGetMessage<InVersionCheck>(out var clientVersion)) {
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
            user.connection.OnReceive.AddListener((InRoomReady msg) => {
                if (msg.ready) {
                    user.room?.GetReady(user);
                } else {
                    user.room?.CancelReady(user);
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