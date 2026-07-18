using RabiRiichi.Actions;
using RabiRiichi.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// Shared base for all AI player agents. Handles the boilerplate common to
  /// every AI (identity, status transitions, state snapshot, and the async
  /// inquiry wrapper) so concrete AIs only implement their decision logic in
  /// <see cref="Decide"/>.
  /// </summary>
  public abstract class AIAgent(int id, Room room, UserStatus status = UserStatus.Playing) : IPlayerAgent {
    /// <summary> The room this agent belongs to (for seat lookup, chat, etc.). </summary>
    protected readonly Room room = room;
    public int id { get; } = id;
    public UserStatus status { get; private set; } = status;
    public int Seat => room.SeatIndexOf(this);

    /// <summary> The concrete AI kind, used for state reporting and nickname. </summary>
    public abstract AiType aiType { get; }

    public virtual string nickname => aiType.ToString().ToUpper();

    public ServerPlayerStateMsg GetState() {
      return new ServerPlayerStateMsg {
        Id = id,
        Nickname = nickname,
        Status = status,
        Seat = Seat,
        AiType = aiType,
      };
    }

    public bool Transit(UserStatus expected, UserStatus next) {
      if (status != expected) {
        return false;
      }
      status = next;
      return true;
    }

    public virtual void OnEvent(EventBase ev) {
      // AIs are stateless across events by default; they decide purely from the
      // public state available at inquiry time. Subclasses (e.g. the LLM agent)
      // may override to accumulate a running transcript.
    }

    public virtual void OnChat(int senderId, string text, string sticker) { }

    public void OnInquiry(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout,
        Action<InquiryResponse> onResponse) {
      // Respond asynchronously so we never block or re-enter the engine's event
      // lock. Any failure falls back to the safe default (skip / tsumogiri).
      Task.Run(() => {
        try {
          onResponse(Decide(gameInquiry, playerInquiry, remainingTimeout));
        } catch (Exception e) {
          Logger.Warn(e);
          onResponse(InquiryResponse.Default(Seat));
        }
      });
    }

    /// <summary> Computes this AI's response to an inquiry. </summary>
    protected abstract InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout);
  }
}
