using RabiRiichi.Action;
using RabiRiichi.Event;

namespace RabiRiichi.Riichi.Setup {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
        void OnEvent(int playerId, EventBase ev);
    }
}