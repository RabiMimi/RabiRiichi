using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;

namespace RabiRiichi.Server.Models {
  public class CreateRoomResp(int id) {
    public int id { get; set; } = id;
  }

  public class Room(Random rand, GameConfig config) {
    public int id;
    public RoomList roomList;
    public Game game { get; private set; }
    public readonly GameConfig config = config;
    public readonly List<IPlayerAgent> players = [];
    private readonly IPlayerAgent[] seats = new IPlayerAgent[config.playerCount];
    private bool isDestroyed = false;
    private readonly Random rand = rand;
    private CancellationTokenSource gameCts;

    private bool HasPlayer(IPlayerAgent player) {
      return players.Any(p => p == player);
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
      if (game != null) {
        msg.Players.AddRange(seats.Select(p => p?.GetState()));
      } else {
        msg.Players.AddRange(players.Select(p => p.GetState()));
      }
      return msg;
    }

    public void BroadcastRoomState() {
      var msg = ProtoUtils.CreateDto(CreateServerRoomStateMsg());
      foreach (var player in players) {
        if (player is User user) {
          user.connection?.Queue(msg);
        }
      }
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
      gameCts = new CancellationTokenSource();
      game = new Game(config);
      // Start the game in a background task and log any exception that bubbles up.
      Task.Run(() => game.Start(gameCts.Token)).ContinueWith(t => {
        if (t.IsFaulted) {
          global::RabiRiichi.Utils.Logger.Warn("[Room] Game task faulted:");
          foreach (var ex in t.Exception!.InnerExceptions) {
            global::RabiRiichi.Utils.Logger.Warn(ex);
          }
        }
        game = null;
        TryEndGame();
        gameCts?.Dispose();
        gameCts = null;
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
      game?.SyncGameStateToPlayer(user.Seat).ContinueWith(t => {
        if (config.actionCenter is ServerActionCenter sac) {
          sac.SyncInquiryTo(user.Seat);
        }
      });
    }

    public bool GetReady(User user) {
      if (!HasPlayer(user)) {
        return false;
      }
      if (!user.Transit(UserStatus.InRoom, UserStatus.Ready)) {
        return false;
      }
      BroadcastRoomState();
      return !players.All(p => p?.status == UserStatus.Ready) || TryStartGame();
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

    public bool AddPlayer(IPlayerAgent player) {
      if (isDestroyed) {
        return false;
      }
      if (players.Count >= config.playerCount) {
        return false;
      }
      if (players.Contains(player)) {
        return false;
      }
      if (player is User user) {
        if (!user.JoinRoom(this)) {
          return false;
        }
      }
      players.Add(player);
      BroadcastRoomState();
      return true;
    }

    public bool RemovePlayer(User user) {
      int seat = SeatIndexOf(user);
      if (!user.ExitRoom(this)) {
        return false;
      }
      if (!players.Remove(user)) {
        return false;
      }
      if (game != null) {
        if (seat >= 0 && seat < seats.Length) {
          seats[seat] = new DefaultAI(user.id, user.nickname + " (AI)", seat);
        }
        if (players.Count(p => p is User) == 0) {
          gameCts?.Cancel();
        }
      }
      BroadcastRoomState();
      if (players.Count(p => p is User) == 0) {
        roomList.Remove(id);
        isDestroyed = true;
      }
      return true;
    }

    public int SeatIndexOf(User user) {
      if (seats.Contains(user)) {
        return Array.IndexOf(seats, user);
      }
      return players.IndexOf(user);
    }

    public IPlayerAgent GetPlayerBySeat(int seatIndex) {
      return seats[seatIndex];
    }
  }
}
