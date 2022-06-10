using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Core;

namespace RabiRiichi.Server.Models {
    public class CreateRoomResp {
        public int id { get; set; }

        public CreateRoomResp(int id) {
            this.id = id;
        }
    }

    public class Room {
        public int id;
        public Game game { get; protected set; }
        public readonly GameConfig config;
        public readonly List<User> players;

        public Room() {
            config = new GameConfig {
                actionCenter = new ServerActionCenter(this),
            };
            players = new List<User>(config.playerCount);
        }

        public Task RunGame() {
            return new Game(config).Start();
        }

        public bool AddPlayer([ModelBinder] User user) {
            lock (players) {
                int emptyIndex = players.IndexOf(null);
                if (emptyIndex < 0) {
                    return false;
                }
                if (players.Contains(user)) {
                    return false;
                }
                if (!user.JoinRoom(this)) {
                    return false;
                }
                players[emptyIndex] = user;
                return true;
            }
        }

        public bool RemovePlayer(User user) {
            lock (players) {
                int index = players.IndexOf(user);
                if (index < 0) {
                    return false;
                }
                players.RemoveAt(index);
                return true;
            }
        }
    }
}
