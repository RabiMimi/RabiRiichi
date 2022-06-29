using RabiRiichi.Generated.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Utils;

namespace RabiRiichi.Server.Connections {
    public static class ConnectionExtensions {
        #region DTO
        public static ServerMessageDto CreateDto(this ServerMsg serverMsg, int respondTo = 0) {
            return new ServerMessageDto {
                RespondTo = respondTo,
                ServerMsg = serverMsg
            };
        }

        public static ServerMessageDto CreateDto(this ServerInquiryMsg inquiry, int respondTo = 0) {
            return new ServerMessageDto {
                RespondTo = respondTo,
                Inquiry = inquiry
            };
        }

        public static ServerMessageDto CreateDto(this EventMsg ev, int respondTo = 0) {
            return new ServerMessageDto {
                RespondTo = respondTo,
                Event = ev
            };
        }

        public static ServerMessageDto CreateDto(this ServerResponse resp, int respondTo = 0) {
            return new ServerMessageDto {
                RespondTo = respondTo,
                ServerResp = resp,
            };
        }
        #endregion

        public static async Task<ClientMessageDto> WaitResponse(this ServerMessageWrapper msg,
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

        public static async Task<bool> HandShake(this RabiStreamingContext ctx) {
            var msg = ctx.connection.Queue(ProtoUtils.CreateDto(new ServerVersionCheckMsg {
                Game = ServerConstants.GAME,
                GameVersion = RabiRiichi.VERSION,
                Server = ServerConstants.SERVER,
                ServerVersion = ServerConstants.SERVER_VERSION,
                MinClientVersion = ServerConstants.MIN_CLIENT_VERSION,
            }));
            var inMsg = await msg.WaitResponse();
            var reply = inMsg.ClientMsg?.VersionCheckMsg;
            if (reply == null) {
                return false;
            }
            if (!ServerUtils.IsClientVersionSupported(reply.ClientVersion)) {
                return false;
            }
            if (!ServerUtils.IsServerVersionSupported(reply.MinServerVersion)) {
                return false;
            }
            return true;
        }

        public static void AddRoomListeners(this User user) {
            user.connection.OnReceive += (ClientMessageDto dto) => {
                var msg = dto.ClientMsg?.RoomUpdateMsg;
                if (msg == null) {
                    return;
                }
                switch (msg.Status) {
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
            };
        }
    }
}