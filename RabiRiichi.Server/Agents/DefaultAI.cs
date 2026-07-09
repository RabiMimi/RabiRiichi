using RabiRiichi.Actions;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Agents {
  /// <summary> A trivial AI that always responds with the default option. </summary>
  public class DefaultAI(int id, Room room, UserStatus status = UserStatus.Playing)
      : AIAgent(id, room, status) {
    public override AiType aiType => AiType.Dummy;

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      return InquiryResponse.Default(Seat);
    }
  }
}
