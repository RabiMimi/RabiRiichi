using RabiRiichi.Server.Messages;

namespace RabiRiichi.Server.Utils {
    public static class Extensions {
        public static bool HandShake(this RabiWSContext ctx) {
            var msg = ctx.connection.CreateMessage(
                OutMsgType.VersionCheck, new OutVersionCheck());
            ctx.Queue(msg);
            if (!msg.WaitResponse.Wait(TimeSpan.FromSeconds(15))) {
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
    }
}