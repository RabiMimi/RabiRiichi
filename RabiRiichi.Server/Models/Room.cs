using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Models {
    public class CreateRoomReq {
        [Required]
        public int sessionCode { get; set; }
    }

    public class CreateRoomResp {
        public int id { get; set; }

        public CreateRoomResp(int id) {
            this.id = id;
        }
    }

    public class Room {
        public int id;
        public Game game;
        public readonly List<Player> players = new();

        public Room() {
            game = new Game(new GameConfig());
        }

        public Player AddPlayer(User user) {
            lock (players) {
                if (players.Count >= game.config.playerCount) {
                    return null;
                }
                if (players.Any(p => p.user == user)) {
                    return null;
                }
                var player = new Player(user, this);
                players.Add(player);
                return player;
            }
        }

        public bool RemovePlayer(User user) {
            lock (players) {
                int index = players.FindIndex(p => p.user == user);
                if (index < 0) {
                    return false;
                }
                players.RemoveAt(index);
                return true;
            }
        }
    }
}
