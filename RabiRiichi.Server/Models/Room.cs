using Microsoft.AspNetCore.Mvc;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Messages;
using RabiRiichi.Server.Utils;
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
        public readonly RoomList roomList;
        private bool isDestroyed = false;

        private bool HasPlayer(User user) {
            return players.Any(p => p == user);
        }

        public void BroadcastRoomState() {
            lock (players) {
                var state = OutRoomState.From(this);
                foreach (var player in players) {
                    player?.connection?.Queue(OutMsgType.RoomState, state);
                }
            }
        }

        public Room(RoomList roomList) {
            this.roomList = roomList;
            config = new GameConfig();
            players = new User[config.playerCount];
        }

        public bool TryStartGame() {
            lock (players) {
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
                BroadcastRoomState();
                Task.Run(() => new Game(config).Start()).ContinueWith(t => {
                    TryEndGame();
                });
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
                BroadcastRoomState();
                return true;
            }
        }

        public RabiWSContext Connect(User user, WebSocket ws) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return null;
                }
                var ctx = user.connection.Connect(ws);
                if (ctx == null) {
                    return null;
                }
                if (config.actionCenter is ServerActionCenter sac) {
                    sac.SyncInquiryTo(user.playerId);
                }
                return ctx;
            }
        }

        public bool GetReady(User user) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return false;
                }
                if (!user.Transit(UserStatus.InRoom, UserStatus.Ready)) {
                    return false;
                }
                BroadcastRoomState();
                if (players.All(p => p?.status == UserStatus.Ready)) {
                    return TryStartGame();
                }
                return true;
            }
        }

        public bool CancelReady(User user) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return false;
                }
                if (!user.Transit(UserStatus.Ready, UserStatus.InRoom)) {
                    return false;
                }
                BroadcastRoomState();
                return true;
            }
        }

        public bool AddPlayer([ModelBinder] User user) {
            lock (players)
                lock (user) {
                    if (isDestroyed) {
                        return false;
                    }
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
                    BroadcastRoomState();
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
                    BroadcastRoomState();
                    if (players.All(p => p == null)) {
                        roomList.Remove(id);
                        isDestroyed = true;
                    }
                    return true;
                }
        }
    }
}
