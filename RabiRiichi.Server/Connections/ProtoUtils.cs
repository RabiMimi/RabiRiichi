using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Server.Connections {
    public static class ProtoUtils {
        public static ServerMessageDto CreateDto<T>(T obj, int respondTo = 0) where T : IMessage<T> {
            try {
                return CreateServerMsg(obj).CreateDto(respondTo);
            } catch (ArgumentException) {
                return CreateServerResponse(obj).CreateDto(respondTo);
            }
        }

        public static ServerResponse CreateServerResponse<T>(T obj) where T : IMessage<T> {
            var ret = new ServerResponse();
            if (obj is GetInfoResponse getInfo) {
                ret.GetInfo = getInfo;
            } else if (obj is CreateUserResponse createUser) {
                ret.CreateUser = createUser;
            } else if (obj is UserInfoResponse getMyInfo) {
                ret.UserInfo = getMyInfo;
            } else if (obj is ServerErrorResponse wsErrorMsg) {
                ret.ServerError = wsErrorMsg;
            } else if (obj is ServerRoomStateResponse roomState) {
                ret.RoomState = roomState;
            } else if (obj is Empty) {
                // noop
            } else {
                throw new ArgumentException("Unknown message type");
            }
            return ret;
        }

        public static ServerMsg CreateServerMsg<T>(T obj) where T : IMessage<T> {
            var ret = new ServerMsg();
            if (obj is TwoWayHeartBeatMsg heartBeat) {
                ret.HeartBeatMsg = heartBeat;
            } else if (obj is ServerRoomStateMsg roomState) {
                ret.RoomStateMsg = roomState;
            } else if (obj is ServerVersionCheckMsg versionCheck) {
                ret.VersionCheckMsg = versionCheck;
            } else if (obj is ServerInquiryMsg inquiry) {
                ret.Inquiry = inquiry;
            } else if (obj is Empty) {
                // noop
            } else {
                throw new ArgumentException("Unknown message type");
            }
            return ret;
        }
    }
}