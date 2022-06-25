using RabiRiichi.Actions;
using RabiRiichi.Events;


namespace RabiRiichi.Communication {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
        void OnEvent(int playerId, EventBase ev);
    }
}