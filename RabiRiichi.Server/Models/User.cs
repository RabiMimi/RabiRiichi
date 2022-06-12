using RabiRiichi.Server.Utils;
using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;

namespace RabiRiichi.Server.Models {
    public class CreateUserReq {
        [Required]
        [StringLength(16, MinimumLength = 1)]
        public string nickname { get; set; }
    }

    public class CreateUserResp {
        public string sessionCode { get; set; }

        public CreateUserResp(long sessionCode) {
            this.sessionCode = sessionCode.ToString("x");
        }
    }

    public class UserInfoResp {
        public int? room { get; set; }

        public UserInfoResp(User user) {
            this.room = user.room?.id;
        }
    }

    public enum UserStatus {
        None = 0,
        InRoom = 1,
        Ready = 2,
        Playing = 3,
    }

    public class User {
        public string nickname = "无名兔兔";

        #region Server logic data members
        public long sessionCode = 0;
        public UserStatus status { get; protected set; } = UserStatus.None;
        public Room room { get; protected set; }
        public int playerId;
        public Connection connection { get; protected set; }
        #endregion

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

        public RabiWSContext Connect(WebSocket ws) {
            return room?.Connect(this, ws);
        }
        #endregion

        #region Room
        public bool JoinRoom(Room room, int playerId) {
            if (!Transit(UserStatus.None, UserStatus.InRoom)) {
                return false;
            }
            this.room = room;
            this.playerId = playerId;
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
