using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Agents;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Services;

namespace RabiRiichi.Server.Models {
  public class CreateRoomResp(int id) {
    public int id { get; set; } = id;
  }

  public class Room(Random rand, GameConfig config, ReplayStore replayStore = null) {
    public int id;
    public RoomList roomList;
    public Game game { get; private set; }
    public readonly GameConfig config = config;
    public readonly List<IPlayerAgent> players = [];
    private readonly IPlayerAgent[] seats = new IPlayerAgent[config.playerCount];
    private bool isDestroyed = false;
    private readonly Random rand = rand;
    public readonly ReplayStore replayStore = replayStore;
    private CancellationTokenSource gameCts;
    private int nextAiId = -101;

    /// <summary>
    /// Allocates a room-lifetime AI identity. IDs are never reused, so removing
    /// and replacing an AI cannot alias chat history or another active player.
    /// Room mutations are serialized by RoomTaskQueue.
    /// </summary>
    public int AllocateAiId() {
      while (players.Any(player => player.id == nextAiId)) {
        nextAiId--;
      }
      return nextAiId--;
    }

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

    public void BroadcastChatMessage(User sender, PlayerChatMessage chatMsg) {
      BroadcastChat(sender.id, chatMsg.Text, chatMsg.Sticker);
    }

    /// <summary>
    /// Broadcasts a chat message on behalf of an in-game agent (e.g. an LLM AI).
    /// Same validation and fan-out as a human message. Safe to call from any
    /// thread; it only enqueues to connections.
    /// </summary>
    public void SendAgentChat(IPlayerAgent sender, string text, string sticker) {
      if (!HasPlayer(sender)) {
        return;
      }
      BroadcastChat(sender.id, text, sticker);
    }

    private void BroadcastChat(int senderId, string text, string sticker) {
      if (isDestroyed) {
        return;
      }

      // Validate sticker path to avoid path traversal / external folder access
      if (!string.IsNullOrEmpty(sticker)) {
        if (sticker.Contains("..") || sticker.StartsWith('/') || sticker.StartsWith('\\') || Path.IsPathRooted(sticker)) {
          sticker = null;
        }
      }

      if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(sticker)) {
        return;
      }

      foreach (var player in players) {
        player.OnChat(senderId, text, sticker);
      }

      var broadcastMsg = new PlayerChatMessage {
        SenderId = senderId,
      };
      if (!string.IsNullOrEmpty(text)) {
        broadcastMsg.Text = text;
      }
      if (!string.IsNullOrEmpty(sticker)) {
        broadcastMsg.Sticker = sticker;
      }

      var msg = ProtoUtils.CreateDto(broadcastMsg);
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
      game.info.gameId = $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{id}";
      // Subscribe before Start so no early events are missed. This tee sees every
      // event once (incl. per-seat [RabiPrivate] ones), fixing the previous
      // seat-0-only capture that dropped other seats' private events.
      game.onGodViewEvent += actionCenter.CaptureGodViewEvent;
      // Start the game in a background task and log any exception that bubbles up.
      Task.Run(() => game.Start(gameCts.Token)).ContinueWith(t => {
        if (t.IsFaulted) {
          global::RabiRiichi.Utils.Logger.Warn("[Room] Game task faulted:");
          foreach (var ex in t.Exception!.InnerExceptions) {
            global::RabiRiichi.Utils.Logger.Warn(ex);
          }
        }
        if (replayStore != null && replayStore.IsEnabled) {
          var sac = config.actionCenter as ServerActionCenter;
          var log = sac?.GetReplayLog();
          if (log != null) {
            try {
              replayStore.SaveReplay(log.GameId, log);
            } catch (Exception ex) {
              global::RabiRiichi.Utils.Logger.Warn($"Failed to save replay: {ex}");
            }
          }
        }
        game = null;
        TryEndGame();
        gameCts?.Dispose();
        gameCts = null;
      });
      return true;
    }

    /// <summary>
    /// Arena-only headless match runner (ARENA_DESIGN.md §10/§11b). Unlike the
    /// normal <see cref="TryStartGame"/> lifecycle — which is private,
    /// fire-and-forget, self-readies AIs for a next game, and saves the replay
    /// with whatever seed the config carried — this drives exactly one game to
    /// completion, uses an Arena-supplied <paramref name="gameId"/>, and invokes
    /// <paramref name="beforeSaveReplay"/> AFTER the game finishes but BEFORE the
    /// replay is saved. That hook is where the Arena stamps the real
    /// <c>RabiRand.seed</c> into the replay log's config (the seed is left null
    /// during play so agents never see it). The awaited task completes only after
    /// the replay has been saved.
    ///
    /// The returned game is exposed via the <paramref name="onGameCreated"/>
    /// callback (called synchronously before the game task starts) so the caller
    /// can read post-game state (final points, the real seed) even though this
    /// method intentionally does NOT null out <see cref="game"/> or re-ready the
    /// agents afterwards — an eval room is single-use and owned by its EvalRoom.
    ///
    /// Requires all <see cref="config"/>.playerCount seats filled with agents
    /// that are InRoom or Ready (this method transitions them to Playing itself,
    /// so callers must NOT pre-ready via <see cref="GetReady"/> — that would
    /// auto-start a normal game). Returns false if that precondition fails.
    /// </summary>
    public async Task<bool> RunHeadlessArenaMatch(
        string gameId,
        Action<Game> onGameCreated,
        Action<Game, ServerActionCenter> beforeSaveReplay,
        CancellationToken cancellationToken = default) {
      if (isDestroyed
          || game != null
          || players.Count != config.playerCount
          || players.Any(p => p.status is not (UserStatus.InRoom or UserStatus.Ready))) {
        return false;
      }
      foreach (var player in players) {
        // Normalize InRoom -> Ready first so the single transition target below
        // is always Ready -> Playing.
        if (player.status == UserStatus.InRoom) {
          player.Transit(UserStatus.InRoom, UserStatus.Ready);
        }
        if (!player.Transit(UserStatus.Ready, UserStatus.Playing)) {
          throw new InvalidOperationException(
              "Player status transition failed, unexpected modification not guarded by lock?");
        }
      }
      SetupSeat();
      ServerActionCenter actionCenter = new(this);
      config.actionCenter = actionCenter;
      BroadcastRoomState();
      using var linkedCts =
          CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      gameCts = linkedCts;
      var startedGame = new Game(config);
      game = startedGame;
      if (!string.IsNullOrEmpty(gameId)) {
        startedGame.info.gameId = gameId;
      }
      // Subscribe before Start so no early events are missed (mirrors TryStartGame).
      startedGame.onGodViewEvent += actionCenter.CaptureGodViewEvent;
      onGameCreated?.Invoke(startedGame);

      try {
        await startedGame.Start(gameCts.Token);
      } catch (Exception ex) {
        global::RabiRiichi.Utils.Logger.Warn("[Room] Arena game task faulted:");
        global::RabiRiichi.Utils.Logger.Warn(ex);
      }

      // End-of-game hook (seed stamp) runs before the replay is persisted (§11b).
      try {
        beforeSaveReplay?.Invoke(startedGame, actionCenter);
      } catch (Exception ex) {
        global::RabiRiichi.Utils.Logger.Warn($"[Room] Arena beforeSaveReplay hook failed: {ex}");
      }

      if (replayStore != null && replayStore.IsEnabled) {
        var log = actionCenter.GetReplayLog();
        if (log != null) {
          try {
            replayStore.SaveReplay(log.GameId, log);
          } catch (Exception ex) {
            global::RabiRiichi.Utils.Logger.Warn($"Failed to save replay: {ex}");
          }
        }
      }
      gameCts = null;
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
      // Auto-ready AIs
      foreach (var player in players) {
        if (player is not User) {
          GetReady(player);
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

    public bool GetReady(IPlayerAgent player) {
      if (!HasPlayer(player)) {
        return false;
      }
      if (!player.Transit(UserStatus.InRoom, UserStatus.Ready)) {
        return false;
      }
      BroadcastRoomState();
      if (players.Count == config.playerCount && players.All(p => p?.status == UserStatus.Ready)) {
        TryStartGame();
      }
      return true;
    }

    public bool CancelReady(IPlayerAgent player) {
      if (!HasPlayer(player)) {
        return false;
      }
      if (!player.Transit(UserStatus.Ready, UserStatus.InRoom)) {
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
      if (players.Contains(player) || players.Any(existing => existing.id == player.id)) {
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
          seats[seat] = new DefaultAI(user.id, this);
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

    /// <summary>
    /// Removes a player from the room. Only valid before the game starts, and
    /// currently only for AI players. Unlike <see cref="RemovePlayer"/>, this
    /// does not touch human-departure logic (ExitRoom / seat substitution /
    /// room destruction); removing a human still goes through
    /// <see cref="RemovePlayer"/>.
    /// </summary>
    public bool RemoveRoomPlayer(IPlayerAgent player) {
      if (game != null) {
        return false;
      }
      // Humans leave via RemovePlayer (which handles ExitRoom / room teardown).
      if (player is User) {
        return false;
      }
      if (!players.Remove(player)) {
        return false;
      }
      BroadcastRoomState();
      return true;
    }

    public int SeatIndexOf(IPlayerAgent player) {
      if (seats.Contains(player)) {
        return Array.IndexOf(seats, player);
      }
      return players.IndexOf(player);
    }

    public IPlayerAgent GetPlayerBySeat(int seatIndex) {
      return seats[seatIndex];
    }
  }
}
