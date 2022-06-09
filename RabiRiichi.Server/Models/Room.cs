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
        public Game game;
        public readonly List<User> players = new();

        public Room() {
            game = new Game(new GameConfig {
                actionCenter = new ServerActionCenter(this),
            });
        }

        public bool AddPlayer([ModelBinder] User user) {
            lock (players) {
                if (players.Count >= game.config.playerCount) {
                    return false;
                }
                if (players.Contains(user)) {
                    return false;
                }
                if (Interlocked.CompareExchange(ref user.room, this, null) != null) {
                    return false;
                }
                players.Add(user);
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
