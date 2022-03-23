using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event;


namespace RabiRiichi.Communication {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
        void OnEvent(int playerId, EventBase ev);
        void OnMessage(Game game, int playerId, IRabiMessage msg);
    }
}