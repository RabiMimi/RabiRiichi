using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

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
  public class RuleBasedAI(int id, Room room, UserStatus status = UserStatus.Playing)
      : AIAgent(id, room, status) {
    public override AiType aiType => AiType.RuleBased;

    protected override InquiryResponse Decide(
        MultiPlayerInquiry gameInquiry,
        SinglePlayerInquiry playerInquiry,
        TimeSpan remainingTimeout) {
      var view = new PublicGameView(gameInquiry.game, Seat);
      return RuleBasedStrategy.Decide(view, playerInquiry);
    }
  }
}
