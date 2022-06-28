using Grpc.Core;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;

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
        private readonly List<User> players = new();
        private readonly User[] seats;
        private bool isDestroyed = false;
        private readonly Random rand;

        private bool HasPlayer(User user) {
            return players.Any(p => p == user);
        }

        private void SetupSeat() {
            for (int i = 0; i < config.playerCount; i++) {
                seats[i] = players[i];
            }
            for (int i = 1; i < config.playerCount; i++) {
                int index = rand.Next(i + 1);
                (seats[i], seats[index]) = (seats[index], seats[i]);
            }
        }

        public void BroadcastRoomState() {
            lock (players) {
                var roomState = new ServerRoomStateMsg {
                    Config = config.ToProto(),
                };
                roomState.Players.AddRange(players.Select(p => p.GetState()));
                var msg = ProtoUtils.CreateServerMsg(roomState).CreateDto();
                foreach (var player in players) {
                    player?.connection?.Queue(msg);
                }
            }
        }

        public Room(Random rand, GameConfig config) {
            this.rand = rand;
            this.config = config;
            seats = new User[config.playerCount];
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

        public RabiStreamingContext Connect(User user,
            IAsyncStreamReader<ClientMessageDto> requestStream,
            IServerStreamWriter<ServerMessageDto> responseStream) {
            lock (players) {
                if (!HasPlayer(user)) {
                    return null;
                }
                var ctx = user.connection.Connect(requestStream, responseStream);
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

        public bool AddPlayer(User user) {
            lock (players)
                lock (user) {
                    if (isDestroyed) {
                        return false;
                    }
                    if (players.Count >= config.playerCount) {
                        return false;
                    }
                    if (players.Contains(user)) {
                        return false;
                    }
                    if (!user.JoinRoom(this)) {
                        return false;
                    }
                    players.Add(user);
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
                    if (!players.Remove(user)) {
                        return false;
                    }
                    BroadcastRoomState();
                    if (players.Count == 0) {
                        roomList.Remove(id);
                        isDestroyed = true;
                    }
                    return true;
                }
        }

        public int SeatIndexOf(User user) {
            lock (players) {
                return players.IndexOf(user);
            }
        }

        public User GetPlayerBySeat(int seatIndex) {
            lock (players) {
                return seats[seatIndex];
            }
        }
    }
}
