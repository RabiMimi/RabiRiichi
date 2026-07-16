using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Events;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Events;
using RabiRiichi.Generated.Core;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Connections {
  public class ServerActionCenter(Room room) : IActionCenter {
    private class InquiryContext(MultiPlayerInquiry inquiry) {
      public readonly MultiPlayerInquiry inquiry = inquiry;
      public readonly DateTime startTime = DateTime.UtcNow;
    }

    private readonly Room room = room;
    private InquiryContext context;
    private GameLogMsg replayLog;
    private readonly object replayLock = new();

    private void EndInquiry(InquiryContext context) {
      var oldContext = Interlocked.CompareExchange(ref this.context, null, context);
      if (oldContext != context) {
        return;
      }
      oldContext.inquiry.Finish();
    }

    public void OnEvent(int seat, EventBase ev) {
      room.GetPlayerBySeat(seat)?.OnEvent(ev);
    }

    /// <summary>
    /// Captures a single god-view (fully revealed) copy of every event for the
    /// replay log. Subscribed to <see cref="Game.onGodViewEvent"/>, so it runs
    /// once per event and is not affected by per-seat [RabiPrivate] filtering
    /// (unlike the per-seat <see cref="OnEvent"/>).
    /// </summary>
    public void CaptureGodViewEvent(EventBase ev) {
      if (room.replayStore == null || !room.replayStore.IsEnabled) {
        return;
      }
      lock (replayLock) {
        EnsureReplayLog();
        var revealedProto = room.game.SerializeProto<EventMsg>(ev, ProtoConverters.GOD_VIEW_PLAYER_ID);
        if (revealedProto != null) {
          replayLog.PlayerLogs[0].Logs.Add(new SingleLogMsg { Event = revealedProto });
        }
      }
    }

    /// <summary>
    /// Captures the per-player inquiries of a single inquiry round into the
    /// replay log, interleaved with events. Inquiries carry the tenpai waits
    /// (candidate <c>tenpaiInfos</c>) that events do not, so the replay client
    /// needs them to show the tenpai indicator. Each inquiry is serialized from
    /// its own owner's seat, since waits are only computed for the acting
    /// player. Called once per inquiry round (from <see cref="OnInquiry"/>), so
    /// reconnect re-syncs (<see cref="SyncInquiryTo"/>) do not duplicate them.
    /// </summary>
    public void CaptureGodViewInquiry(MultiPlayerInquiry inquiry) {
      if (room.replayStore == null || !room.replayStore.IsEnabled) {
        return;
      }
      lock (replayLock) {
        EnsureReplayLog();
        foreach (var playerInquiry in inquiry.playerInquiries) {
          var proto = room.game.SerializeProto<SinglePlayerInquiryMsg>(
              playerInquiry, playerInquiry.playerId);
          if (proto != null) {
            replayLog.PlayerLogs[0].Logs.Add(new SingleLogMsg { Inquiry = proto });
          }
        }
      }
    }

    /// <summary> Lazily creates the replay log. Must be called under replayLock. </summary>
    private void EnsureReplayLog() {
      if (replayLog != null) {
        return;
      }
      replayLog = new GameLogMsg {
        GameId = room.game.info.gameId,
        CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Config = room.game.config.ToProto(),
      };
      foreach (var player in room.players) {
        var playerState = player.GetState();
        replayLog.Players.Add(new GameLogPlayerMsg {
          Id = player.id,
          Nickname = player.nickname,
          Seat = player.Seat,
          AiType = playerState.AiType
        });
      }
      replayLog.PlayerLogs.Add(new PlayerLogMsg());
    }

    public GameLogMsg GetReplayLog() {
      lock (replayLock) {
        return replayLog;
      }
    }

    private void SendInquiry(InquiryContext ctx, int seat) {
      var inquiry = ctx?.inquiry.GetByPlayerId(seat);
      if (inquiry == null) {
        return;
      }
      var player = room.GetPlayerBySeat(seat);
      if (player == null) {
        return;
      }
      var remaining = TimeSpan.Zero;
      if (ctx.inquiry.timeout > TimeSpan.Zero) {
        var elapsed = DateTime.UtcNow - ctx.startTime;
        remaining = ctx.inquiry.timeout - elapsed;
        if (remaining < TimeSpan.Zero) {
          remaining = TimeSpan.Zero;
        }
      }
      player.OnInquiry(ctx.inquiry, inquiry, remaining, resp => {
        if (ctx.inquiry.OnResponse(resp)) {
          EndInquiry(ctx);
        }
      });
    }

    public void SyncInquiryTo(int seat) {
      SendInquiry(context, seat);
    }

    public void OnInquiry(MultiPlayerInquiry inquiry) {
      var ctx = new InquiryContext(inquiry);
      // If a previous inquiry has already finished (hasExecuted), clear the stale context
      // before trying to register the new one. This prevents the “Inquiry already in progress”
      // race condition.
      var currentContext = context;
      if (currentContext != null && currentContext.inquiry.hasExecuted) {
        Interlocked.CompareExchange(ref context, null, currentContext);
      }
      if (Interlocked.CompareExchange(ref context, ctx, null) != null) {
        throw new InvalidOperationException("Inquiry already in progress");
      }
      // Record the inquiry (with its tenpai waits) into the replay log before
      // sending it out. Skip empty inquiries: they carry no actions/waits.
      if (!inquiry.IsEmpty) {
        CaptureGodViewInquiry(inquiry);
      }
      for (int i = 0; i < inquiry.playerInquiries.Count; i++) {
        var playerInquiry = inquiry.playerInquiries[i];
        SendInquiry(ctx, playerInquiry.playerId);
      }
      if (inquiry.IsEmpty) {
        EndInquiry(ctx);
      }
    }
  }
}