using RabiRiichi.Communication.Proto;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Communication.Sync;
using RabiRiichi.Actions;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Communication.Sync {
  [RabiMessage]
  public class PlayerHandState(Hand hand, int playerId) : IRabiPlayerMessage {
    public int playerId { get; init; } = playerId;
    [RabiPrivate] public readonly List<GameTile> freeTiles = [.. hand.freeTiles];
    [RabiPrivate] public readonly GameTile pendingTile = hand.pendingTile;
    [RabiBroadcast] public readonly List<MenLike> called = [.. hand.called];
    [RabiBroadcast] public readonly List<GameTile> discarded = [.. hand.discarded];
    [RabiBroadcast] public readonly List<GameTile> nukiDora = [.. hand.nukiDora];
    [RabiBroadcast] public readonly int jun = hand.jun;
    [RabiBroadcast] public readonly int riichiStick = hand.riichiStick;
    [RabiBroadcast] public readonly GameTile agariTile = hand.agariTile;
    [RabiBroadcast] public readonly GameTile riichiTile = hand.riichiTile;
    [RabiPrivate] public readonly bool isTempFuriten = hand.isTempFuriten;
    [RabiPrivate] public readonly bool isRiichiFuriten = hand.isRiichiFuriten;
    [RabiPrivate] public readonly bool isDiscardFuriten = hand.isDiscardFuriten;

    public PlayerHandStateMsg ToProto(int playerId) {
      var ret = new PlayerHandStateMsg {
        Jun = jun,
        RiichiStick = riichiStick,
        AgariTile = ProtoConverters.ConvertGameTile(agariTile, true),
        RiichiTile = ProtoConverters.ConvertGameTile(riichiTile, true),
        PendingTile = ProtoConverters.ConvertGameTile(pendingTile, this.playerId == playerId),
      };
      ret.Called.AddRange(called.Select(x => x.ToProto()));
      ret.Discarded.AddRange(discarded.Select(tile => ProtoConverters.ConvertGameTile(tile, true)));
      ret.NukiDora.AddRange(nukiDora.Select(tile => ProtoConverters.ConvertGameTile(tile, true)));
      ret.FreeTiles.AddRange(freeTiles.Select(tile => ProtoConverters.ConvertGameTile(tile, this.playerId == playerId)));
      if (this.playerId == playerId) {
        ret.IsTempFuriten = isTempFuriten;
        ret.IsRiichiFuriten = isRiichiFuriten;
        ret.IsDiscardFuriten = isDiscardFuriten;

        var patternResolver = hand.game.Get<PatternResolver>();
        var tenpaiTiles = hand.Tenpai;
        if (tenpaiTiles.Count > 0) {
          foreach (var winTile in tenpaiTiles) {
            var info = PlayTileAction.ComputeTenpaiInfo(patternResolver, hand, winTile);
            ret.TenpaiWaits.Add(ProtoConverters.ConvertTenpaiInfo(info));
          }
        }
      }
      return ret;
    }
  }

  [RabiMessage]
  public class WallState(Wall wall) {
    [RabiBroadcast] public readonly List<GameTile> doras = [.. wall.doras.Take(wall.revealedDoraCount)];
    [RabiBroadcast] public readonly int remaining = wall.NumRemaining;
    [RabiBroadcast] public readonly int rinshanRemaining = wall.rinshan.Count;

    public WallStateMsg ToProto() {
      var ret = new WallStateMsg {
        Remaining = remaining,
        RinshanRemaining = rinshanRemaining,
      };
      ret.Doras.AddRange(doras.Select(dora => ProtoConverters.ConvertGameTile(dora, true)));
      return ret;
    }
  }

  [RabiMessage]
  public class PlayerState(Player player, int receiverId) : IRabiPlayerMessage {
    public int playerId { get; init; } = receiverId;
    [RabiBroadcast] public readonly int id = player.id;
    [RabiBroadcast] public readonly long points = player.points;
    [RabiBroadcast] public readonly PlayerHandState hand = new PlayerHandState(player.hand, receiverId);

    public PlayerStateMsg ToProto(int playerId) {
      return new PlayerStateMsg {
        Id = id,
        Points = points,
        Hand = hand.ToProto(playerId)
      };
    }
  }

  [RabiMessage]
  public class GameState(Game game, int playerId) : IRabiPlayerMessage {
    public int playerId { get; init; } = playerId;

    [RabiBroadcast] public readonly GameConfig config = game.config;
    [RabiBroadcast] public readonly GameInfo info = game.info;
    [RabiBroadcast] public readonly WallState wall = new WallState(game.wall);
    [RabiBroadcast] public readonly PlayerState[] players = [.. game.players.Select(p => new PlayerState(p, playerId))];
    [RabiBroadcast] public readonly int currentPlayer = game.info.currentPlayer;

    public GameStateMsg ToProto(int playerId) {
      var ret = new GameStateMsg {
        Config = config.ToProto(),
        Info = info.ToProto(),
        Wall = wall.ToProto(),
        CurrentPlayer = currentPlayer,
      };
      ret.Players.AddRange(players.Select(x => x.ToProto(playerId)));
      return ret;
    }
  }
}