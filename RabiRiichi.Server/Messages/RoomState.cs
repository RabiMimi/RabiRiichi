using RabiRiichi.Core.Config;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Messages {
    public class OutPlayerState {
        public int id;
        public string nickname;
        public UserStatus status;

        public static OutPlayerState From(User player) {
            return player == null ? null : new OutPlayerState {
                id = player.seat,
                nickname = player.nickname,
                status = player.status,
            };
        }
    }

    public class OutRoomState {
        public List<OutPlayerState> players;
        public GameConfig config;

        public static OutRoomState From(Room room) {
            return room == null ? null : new OutRoomState {
                players = room.users.Select(p => OutPlayerState.From(p)).ToList(),
                config = room.config,
            };
        }
    }
}