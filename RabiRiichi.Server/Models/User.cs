using RabiRiichi.Actions;
using RabiRiichi.Events;
using RabiRiichi.Generated.Events;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Utils;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

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

  public class User : IPlayerAgent {
    public int id { get; set; }
    public string nickname { get; set; } = "无名兔兔";

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
        AiType = AiType.None,
      };
    }

    public void OnEvent(EventBase ev) {
      var proto = ev.game.SerializeProto<EventMsg>(ev, Seat);
      if (proto != null) {
        connection?.Queue(proto.CreateDto());
      }
    }

    public void OnChat(int senderId, string text, string sticker) { }

    public void OnInquiry(MultiPlayerInquiry gameInquiry, SinglePlayerInquiry playerInquiry, TimeSpan remainingTimeout, Action<InquiryResponse> onResponse) {
      var singlePlayerInquiryMsg = gameInquiry.game.SerializeProto<SinglePlayerInquiryMsg>(playerInquiry, Seat);
      if (singlePlayerInquiryMsg != null && remainingTimeout > TimeSpan.Zero) {
        singlePlayerInquiryMsg.TimeoutSeconds = remainingTimeout.TotalSeconds;
      }
      var inquiryMsg = new ServerInquiryMsg {
        Inquiry = singlePlayerInquiryMsg
      };
      var msg = connection?.Queue(ProtoUtils.CreateDto(inquiryMsg));
      if (msg != null) {
        async Task WaitResponse() {
          var waitAny = await Task.WhenAny(
              gameInquiry.WaitForFinish,
              msg.WaitResponse(TimeSpan.FromHours(1)));
          if (waitAny is not Task<ClientMessageDto> responseTask) {
            return;
          }
          var resp = responseTask.Result?.ClientMsg?.InquiryMsg;
          var inquiryResp = resp == null ? InquiryResponse.Default(Seat)
              : new InquiryResponse(Seat, resp.Index, resp.Response);
          onResponse(inquiryResp);
        }
        _ = WaitResponse();
      } else {
        onResponse(InquiryResponse.Default(Seat));
      }
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
