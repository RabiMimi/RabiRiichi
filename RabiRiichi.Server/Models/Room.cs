using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Connections;
using System.Net.WebSockets;

namespace RabiRiichi.Server.Models {
    public class CreateRoomResp {
        public int id { get; set; }

        public CreateRoomResp(int id) {
            this.id = id;
        }
    }

    public class Room {
        public int id;
        public readonly GameConfig config;
        public readonly User[] players;

        private bool HasPlayer(User user) {
            return players.Any(p => p == user);
        }

        public Room() {
            config = new GameConfig();
            players = new User[config.playerCount];
        }

        public bool TryStartGame(out Task task) {
            lock (players) {
                task = null;
                if (players.Any(p => p == null || p.status != UserStatus.Ready)) {
                    return false;
                }
                foreach (var player in players) {
                    if (!player.Transit(UserStatus.Ready, UserStatus.Playing)) {
                        throw new InvalidOperationException(
                            $"Player status transition failed, unexpected modification not guarded by lock?");
                    }
                }
                ServerActionCenter actionCenter = new(this);
                config.actionCenter = actionCenter;
                task = new Game(config).Start();
                return true;
            }
        }

        public bool TryEndGame() {
            lock (players) {
                if (players.Any(p => p == null || p.status != UserStatus.Playing)) {
                    return false;
                }
                foreach (var player in players) {
                    if (!player.Transit(UserStatus.Playing, UserStatus.InRoom)) {
                        throw new InvalidOperationException(
                            $"Player status transition failed, unexpected modification not guarded by lock?");
                    }
                }
                return true;
            }
        }

        public RabiWSContext Connect(User user, WebSocket ws) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return null;
                }
                return user.connection.Connect(ws);
            }
        }

        public bool GetReady(User user) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return false;
                }
                return user.Transit(UserStatus.InRoom, UserStatus.Ready);
            }
        }

        public bool CancelReady(User user) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return false;
                }
                return user.Transit(UserStatus.Ready, UserStatus.InRoom);
            }
        }

        public bool AddPlayer([ModelBinder] User user) {
            lock (players)
                lock (user) {
                    int emptyIndex = Array.IndexOf(players, null);
                    if (emptyIndex < 0) {
                        return false;
                    }
                    if (players.Contains(user)) {
                        return false;
                    }
                    if (!user.JoinRoom(this, emptyIndex)) {
                        return false;
                    }
                    players[emptyIndex] = user;
                    return true;
                }
        }

        public bool RemovePlayer(User user) {
            lock (players)
                lock (user) {
                    if (!user.ExitRoom(this)) {
                        return false;
                    }
                    int index = Array.IndexOf(players, user);
                    if (index < 0) {
                        throw new InvalidOperationException("Player not found in room");
                    }
                    players[index] = null;
                    return true;
                }
        }
    }
}
