using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Generated.Messages;
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
        public RoomList roomList;
        public readonly GameConfig config;
        public readonly User[] users;
        public readonly User[] seats;
        private bool isDestroyed = false;
        private readonly Random rand;

        private bool HasPlayer(User user) {
            return users.Any(p => p == user);
        }

        private void SetupSeat() {
            for (int i = 0; i < config.playerCount; i++) {
                seats[i] = users[i];
            }
            for (int i = 1; i < config.playerCount; i++) {
                int index = rand.Next(i + 1);
                (seats[i], seats[index]) = (seats[index], seats[i]);
            }
        }

        public void BroadcastRoomState() {
            lock (users) {
                var state = OutRoomState.From(this);
                foreach (var player in users) {
                    player?.connection?.Queue(OutMsgType.RoomState, state);
                }
            }
        }

        public Room(Random rand, GameConfig config) {
            this.rand = rand;
            this.config = config;
            users = new User[config.playerCount];
            seats = new User[config.playerCount];
        }

        public bool TryStartGame() {
            lock (users) {
                if (users.Any(p => p == null || p.status != UserStatus.Ready)) {
                    return false;
                }
                foreach (var player in users) {
                    if (!player.Transit(UserStatus.Ready, UserStatus.Playing)) {
                        throw new InvalidOperationException(
                            $"Player status transition failed, unexpected modification not guarded by lock?");
                    }
                }
                SetupSeat();
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
            lock (users) {
                if (users.Any(p => p == null || p.status != UserStatus.Playing)) {
                    return false;
                }
                foreach (var player in users) {
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
            lock (users) {
                if (!HasPlayer(user)) {
                    return null;
                }
                var ctx = user.connection.Connect(ws);
                if (ctx == null) {
                    return null;
                }
                if (config.actionCenter is ServerActionCenter sac) {
                    sac.SyncInquiryTo(user.seat);
                }
                return ctx;
            }
        }

        public bool GetReady(User user) {
            lock (users) {
                if (!HasPlayer(user)) {
                    return false;
                }
                if (!user.Transit(UserStatus.InRoom, UserStatus.Ready)) {
                    return false;
                }
                BroadcastRoomState();
                if (users.All(p => p?.status == UserStatus.Ready)) {
                    return TryStartGame();
                }
                return true;
            }
        }

        public bool CancelReady(User user) {
            lock (users) {
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

        public bool AddPlayer(User user) {
            lock (users)
                lock (user) {
                    if (isDestroyed) {
                        return false;
                    }
                    int emptyIndex = Array.IndexOf(users, null);
                    if (emptyIndex < 0) {
                        return false;
                    }
                    if (users.Contains(user)) {
                        return false;
                    }
                    if (!user.JoinRoom(this)) {
                        return false;
                    }
                    users[emptyIndex] = user;
                    BroadcastRoomState();
                    return true;
                }
        }

        public bool RemovePlayer(User user) {
            lock (users)
                lock (user) {
                    if (!user.ExitRoom(this)) {
                        return false;
                    }
                    int index = Array.IndexOf(users, user);
                    if (index < 0) {
                        throw new InvalidOperationException("Player not found in room");
                    }
                    users[index] = null;
                    BroadcastRoomState();
                    if (users.All(p => p == null)) {
                        roomList.Remove(id);
                        isDestroyed = true;
                    }
                    return true;
                }
        }

        public int SeatIndexOf(User user) {
            lock (users) {
                return Array.IndexOf(users, user);
            }
        }

        public User GetPlayerBySeat(int seatIndex) {
            lock (users) {
                return seats[seatIndex];
            }
        }
    }
}
