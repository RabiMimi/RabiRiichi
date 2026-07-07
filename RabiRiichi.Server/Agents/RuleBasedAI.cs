using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;
using RabiRiichi.Utils;

namespace RabiRiichi.Server.Agents {
  /// <summary>
  /// A master-level, rule-based mahjong agent.
  ///
  /// It plays a strong, principled game using ONLY publicly available
  /// information (its own hand plus everything a fair opponent could observe -
  /// discards, called melds, revealed dora, riichi status, the wall count). It
  /// never reads hidden state (opponents' concealed tiles, the wall order,
  /// unrevealed dora/uradora); all game access goes through
  /// <see cref="PublicGameView"/> and the per-seat inquiry, both of which are
  /// restricted to fair information.
  ///
  /// Decision policy, in priority order:
  ///  - Win whenever a valid ron/tsumo is offered.
  ///  - When tenpai and menzen, declare riichi if the wait is worthwhile.
  ///  - On a discard, balance hand efficiency (shanten / acceptance from the
  ///    server-computed candidates) against safety versus riichi opponents.
  ///  - Call (chii/pon/kan) only when it clearly advances a yaku-bearing hand;
  ///    otherwise stay concealed.
  /// </summary>
  public class RuleBasedAI(int id, Room room, UserStatus status = UserStatus.Playing) : IPlayerAgent {
    public int id { get; } = id;
    public AiType aiType => AiType.RuleBased;

    public string nickname => aiType.ToString().ToUpper();

    public UserStatus status { get; private set; } = status;
    public int Seat => room.SeatIndexOf(this);

    public ServerPlayerStateMsg GetState() {
      return new ServerPlayerStateMsg {
        Id = id,
        Nickname = nickname,
        Status = status,
        Seat = Seat,
        AiType = AiType.RuleBased,
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
      // The strategy is stateless across events; it decides purely from the
      // public state available at inquiry time.
    }

    public void OnInquiry(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout,
        Action<InquiryResponse> onResponse) {
      // Respond asynchronously so we never block or re-enter the engine's event
      // lock (mirrors DefaultAI).
      Task.Run(() => {
        try {
          var view = new PublicGameView(gameInquiry.game, Seat);
          var decision = RuleBasedStrategy.Decide(view, playerInquiry);
          onResponse(decision);
        } catch (Exception e) {
          Logger.Warn(e);
          // Fall back to the safe default (skip / tsumogiri) on any error.
          onResponse(InquiryResponse.Default(Seat));
        }
      });
    }
  }
}
