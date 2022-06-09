using RabiRiichi.Action;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Event;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Core {
    public class ServerActionCenter : IActionCenter {
        public readonly Room room;
        public MultiPlayerInquiry currentInquiry;

        public ServerActionCenter(Room room) {
            this.room = room;
        }

        public void OnEvent(int playerId, EventBase ev) {
            throw new NotImplementedException();
        }

        public void OnInquiry(MultiPlayerInquiry inquiry) {
            currentInquiry = inquiry;
            throw new NotImplementedException();
        }

        public void OnMessage(Game game, int playerId, IRabiMessage msg) {
            throw new NotImplementedException();
        }
    }
}