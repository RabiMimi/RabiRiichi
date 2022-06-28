using Grpc.Core;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
    public class CreateUserReq {
        [Required]
        [StringLength(16, MinimumLength = 1)]
        public string nickname { get; set; }
    }

    public class UserInfoResp {
        public int id { get; set; }
        public int? room { get; set; }
        public string nickname { get; set; }
        public UserStatus status { get; set; }

        public UserInfoResp(User user) {
            id = user.seat;
            room = user.room?.id;
            nickname = user.nickname;
            status = user.status;
        }
    }

    public class User {
        public int id;
        public string nickname = "无名兔兔";

        #region Server logic data members
        public UserStatus status { get; protected set; } = UserStatus.None;
        public Room room { get; protected set; }
        public int seat => room.SeatIndexOf(this);
        public Connection connection { get; protected set; }
        #endregion

        public ServerPlayerStateMsg GetState() {
            return new ServerPlayerStateMsg {
                Id = id,
                Nickname = nickname,
                Status = status,
                Seat = seat,
            };
        }

        #region Game
        public bool Transit(UserStatus expected, UserStatus next) {
            if (status != expected) {
                return false;
            }
            status = next;
            return true;
        }

        protected void SetConnection(bool clear = false) {
            if (connection != null) {
                connection.Current?.Close();
                connection.Dispose();
            }
            if (clear) {
                connection = null;
            } else {
                connection = new Connection(this);
                this.AddRoomListeners();
            }
        }

        public RabiStreamingContext Connect(
            IAsyncStreamReader<ClientMessageDto> requestStream,
            IServerStreamWriter<ServerMessageDto> responseStream) {
            return room?.Connect(this, requestStream, responseStream);
        }
        #endregion

        #region Room
        public bool JoinRoom(Room room) {
            if (!Transit(UserStatus.None, UserStatus.InRoom)) {
                return false;
            }
            this.room = room;
            SetConnection();
            return true;
        }

        public bool ExitRoom(Room room) {
            if (this.room != room || !Transit(UserStatus.InRoom, UserStatus.None)) {
                return false;
            }
            this.room = null;
            SetConnection(true);
            return true;
        }
        #endregion
    }
}
