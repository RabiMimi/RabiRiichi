using RabiRiichi.Server.Core;
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
        None,
        InRoom,
        Ready,
        Playing,
    }

    public class User {
        public string nickname = "无名兔兔";

        #region Server logic data members
        public long sessionCode = 0;
        public UserStatus status { get; protected set; } = UserStatus.None;
        public Room room { get; protected set; }
        public Connection connection { get; protected set; }
        #endregion

        #region Game
        protected bool Transit(UserStatus expected, UserStatus next) {
            if (status != expected) {
                return false;
            }
            status = next;
            return true;
        }

        protected void SetConnection(bool clear = false) {
            connection?.Dispose();
            if (clear) {
                connection = null;
            } else {
                connection = new Connection(this);
            }
        }

        public bool StopGame() {
            lock (this) {
                return Transit(UserStatus.Playing, UserStatus.InRoom);
            }
        }

        public bool StartGame() {
            lock (this) {
                return Transit(UserStatus.Ready, UserStatus.Playing);
            }
        }

        public bool GetReady() {
            lock (this) {
                return Transit(UserStatus.InRoom, UserStatus.Ready);
            }
        }

        public bool ResetReady() {
            lock (this) {
                return Transit(UserStatus.Ready, UserStatus.InRoom);
            }
        }

        public bool Connect(WebSocket ws) {
            return connection.Connect(ws);
        }
        #endregion

        #region Room
        public bool JoinRoom(Room room) {
            lock (this) {
                if (!Transit(UserStatus.None, UserStatus.InRoom)) {
                    return false;
                }
                this.room = room;
                SetConnection();
            }
            return true;
        }

        public bool ExitRoom() {
            lock (this) {
                if (!Transit(UserStatus.InRoom, UserStatus.None)) {
                    return false;
                }
                room = null;
                SetConnection(true);
            }
            return true;
        }
        #endregion
    }
}
