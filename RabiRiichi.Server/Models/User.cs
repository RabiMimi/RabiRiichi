using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
  public class CreateUserReq {
    [Required]
    [StringLength(16, MinimumLength = 1)]
    public string nickname { get; set; }
  }

  public class UserInfoResp(User user) {
    public int id { get; set; } = user.Seat;
    public int? room { get; set; } = user.room?.id;
    public string nickname { get; set; } = user.nickname;
    public UserStatus status { get; set; } = user.status;
  }

  public class User {
    public int id;
    public string nickname = "无名兔兔";

    #region Server logic data members
    public UserStatus status { get; protected set; } = UserStatus.None;
    public Room room { get; protected set; }
    public int Seat => room.SeatIndexOf(this);
    public readonly Connection connection = new();
    #endregion

    public ServerPlayerStateMsg GetState() {
      return new ServerPlayerStateMsg {
        Id = id,
        Nickname = nickname,
        Status = status,
        Seat = Seat,
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
    #endregion

    #region Room
    public bool JoinRoom(Room room) {
      if (!Transit(UserStatus.None, UserStatus.InRoom)) {
        return false;
      }
      this.room = room;
      return true;
    }

    public bool ExitRoom(Room room) {
      if (this.room != room || (
          !Transit(UserStatus.InRoom, UserStatus.None)
          && !Transit(UserStatus.Ready, UserStatus.None)
          && !Transit(UserStatus.Playing, UserStatus.None))) {
        return false;
      }
      this.room = null;
      return true;
    }
    #endregion
  }
}