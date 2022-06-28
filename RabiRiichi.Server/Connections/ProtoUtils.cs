using RabiRiichi.Server.Generated.Messages;

namespace RabiRiichi.Server.Connections {
    public static class ProtoUtils {
        public static ServerMsg CreateServerMsg<T>(T obj) where T : class {
            var ret = new ServerMsg();
            if (obj is TwoWayHeartBeatMsg heartBeat) {
                ret.HeartBeatMsg = heartBeat;
            } else if (obj is ServerInquiryEndMsg inquiryEnd) {
                ret.InquiryEndMsg = inquiryEnd;
            } else if (obj is ServerPlayerStateMsg playerState) {
                ret.PlayerStateMsg = playerState;
            } else if (obj is ServerRoomStateMsg roomState) {
                ret.RoomStateMsg = roomState;
            } else if (obj is ServerVersionCheckMsg versionCheck) {
                ret.VersionCheckMsg = versionCheck;
            } else {
                throw new ArgumentException("Unknown message type");
            }
            return ret;
        }
    }
}