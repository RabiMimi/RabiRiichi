using RabiRiichi.Actions;
using RabiRiichi.Events;
using RabiRiichi.Server.Generated.Messages;
using System;

namespace RabiRiichi.Server.Agents {
  public interface IPlayerAgent {
    int id { get; }
    string nickname { get; }
    UserStatus status { get; }
    int Seat { get; }
    ServerPlayerStateMsg GetState();
    bool Transit(UserStatus expected, UserStatus next);
    void OnEvent(EventBase ev);
    void OnChat(int senderId, string text, string sticker);
    void OnInquiry(MultiPlayerInquiry gameInquiry, SinglePlayerInquiry playerInquiry, TimeSpan remainingTimeout, Action<InquiryResponse> onResponse);
  }
}
