using RabiRiichi.Action;
using RabiRiichi.Event;

namespace RabiRiichi.Interact {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
        void OnEvent(int playerId, EventBase ev);
    }
}