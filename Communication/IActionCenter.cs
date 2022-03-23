using RabiRiichi.Action;
using RabiRiichi.Event;
using RabiRiichi.Riichi;


namespace RabiRiichi.Communication {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
        void OnEvent(int playerId, EventBase ev);
        void OnMessage(Game game, int playerId, IRabiMessage msg);
    }
}