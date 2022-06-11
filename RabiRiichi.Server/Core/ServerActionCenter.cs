using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
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

        public void OnMessage(int playerId, object msg) {
            throw new NotImplementedException();
        }
    }
}