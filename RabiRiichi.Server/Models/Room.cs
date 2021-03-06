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
        public Game game { get; private set; }
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

        public ServerRoomStateMsg CreateServerRoomStateMsg() {
            var msg = new ServerRoomStateMsg {
                Id = id,
                Config = config.ToProto(),
            };
            msg.Players.AddRange(players.Select(p => p.GetState()));
            return msg;
        }

        public void BroadcastRoomState() {
            var msg = ProtoUtils.CreateDto(CreateServerRoomStateMsg());
            foreach (var player in players) {
                player.connection?.Queue(msg);
            }
        }

        public Room(Random rand, GameConfig config) {
            this.rand = rand;
            this.config = config;
            seats = new User[config.playerCount];
        }

        private bool TryStartGame() {
            if (players.Count != config.playerCount
                || players.Any(p => p.status != UserStatus.Ready)) {
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
            game = new Game(config);
            Task.Run(() => game.Start()).ContinueWith(t => {
                game = null;
                TryEndGame();
            });
            return true;
        }

        private bool TryEndGame() {
            if (players.Any(p => p.status != UserStatus.Playing)) {
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

        public void SyncGameTo(User user) {
            if (!HasPlayer(user) || user.Seat < 0) {
                return;
            }
            if (game != null) {
                game.SyncGameStateToPlayer(user.Seat).ContinueWith(t => {
                    if (config.actionCenter is ServerActionCenter sac) {
                        sac.SyncInquiryTo(user.Seat);
                    }
                });
            }
        }

        public bool GetReady(User user) {
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

        public bool CancelReady(User user) {
            if (!HasPlayer(user)) {
                return false;
            }
            if (!user.Transit(UserStatus.Ready, UserStatus.InRoom)) {
                return false;
            }
            BroadcastRoomState();
            return true;
        }

        public bool AddPlayer(User user) {
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

        public bool RemovePlayer(User user) {
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

        public int SeatIndexOf(User user) {
            return players.IndexOf(user);
        }

        public User GetPlayerBySeat(int seatIndex) {
            return seats[seatIndex];
        }
    }
}
