using Google.Protobuf.WellKnownTypes;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Server.Connections {
    public static class ProtoUtils {
        public static ServerMessageDto CreateDto<T>(T obj, int respondTo = 0) where T : class {
            return CreateServerMsg(obj).CreateDto(respondTo);
        }

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
            } else if (obj is ServerWSErrorMsg wsErrorMsg) {
                ret.WsErrorMsg = wsErrorMsg;
            } else if (obj is Empty) {
                // noop
            } else {
                throw new ArgumentException("Unknown message type");
            }
            return ret;
        }
    }
}