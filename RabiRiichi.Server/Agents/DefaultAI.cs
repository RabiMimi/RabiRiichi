using RabiRiichi.Actions;
using RabiRiichi.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Agents {
  public class DefaultAI(int id, Room room, UserStatus status = UserStatus.Playing) : IPlayerAgent {
    public int id { get; } = id;
    public AiType aiType => AiType.Dummy;
    public string nickname => aiType.ToString().ToUpper();
    public UserStatus status { get; private set; } = status;
    public int Seat => room.SeatIndexOf(this);

    public ServerPlayerStateMsg GetState() {
      return new ServerPlayerStateMsg {
        Id = id,
        Nickname = nickname,
        Status = status,
        Seat = Seat,
        AiType = AiType.Dummy,
      };
    }

    public bool Transit(UserStatus expected, UserStatus next) {
      if (status != expected) {
        return false;
      }
      status = next;
      return true;
    }

    public void OnEvent(EventBase ev) {
      // AI does not process events
    }

    public void OnInquiry(MultiPlayerInquiry gameInquiry, SinglePlayerInquiry playerInquiry, TimeSpan remainingTimeout, Action<InquiryResponse> onResponse) {
      // Respond with default option asynchronously to prevent stack overflow from synchronous recursion.
      Task.Run(() => {
        onResponse(InquiryResponse.Default(Seat));
      });
    }
  }
}
