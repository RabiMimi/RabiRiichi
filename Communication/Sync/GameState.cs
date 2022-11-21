using RabiRiichi.Communication.Proto;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Communication.Sync;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Communication.Sync {
    [RabiMessage]
    public class PlayerHandState : IRabiPlayerMessage {
        public int playerId { get; init; }
        [RabiPrivate] public readonly List<GameTile> freeTiles;
        [RabiBroadcast] public readonly List<MenLike> called;
        [RabiBroadcast] public readonly List<GameTile> discarded;
        [RabiBroadcast] public readonly int jun;
        [RabiBroadcast] public readonly int riichiStick;
        [RabiBroadcast] public readonly GameTile agariTile;
        [RabiBroadcast] public readonly GameTile riichiTile;
        [RabiPrivate] public readonly bool isTempFuriten;
        [RabiPrivate] public readonly bool isRiichiFuriten;
        [RabiPrivate] public readonly bool isDiscardFuriten;
        public PlayerHandState(Hand hand, int playerId) {
            this.playerId = playerId;
            freeTiles = hand.freeTiles.ToList();
            called = hand.called.ToList();
            discarded = hand.discarded.ToList();
            jun = hand.jun;
            riichiStick = hand.riichiStick;
            agariTile = hand.agariTile;
            riichiTile = hand.riichiTile;
            isTempFuriten = hand.isTempFuriten;
            isRiichiFuriten = hand.isRiichiFuriten;
            isDiscardFuriten = hand.isDiscardFuriten;
        }

        public PlayerHandStateMsg ToProto(int playerId) {
            var ret = new PlayerHandStateMsg {
                Jun = jun,
                RiichiStick = riichiStick,
                AgariTile = ProtoConverters.ConvertGameTile(agariTile, true),
                RiichiTile = ProtoConverters.ConvertGameTile(riichiTile, true),
            };
            ret.Called.AddRange(called.Select(x => x.ToProto()));
            ret.Discarded.AddRange(discarded.Select(tile => ProtoConverters.ConvertGameTile(tile, this.playerId == playerId)));
            ret.FreeTiles.AddRange(freeTiles.Select(tile => ProtoConverters.ConvertGameTile(tile, this.playerId == playerId)));
            if (this.playerId == playerId) {
                ret.IsTempFuriten = isTempFuriten;
                ret.IsRiichiFuriten = isRiichiFuriten;
                ret.IsDiscardFuriten = isDiscardFuriten;
            }
            return ret;
        }
    }

    [RabiMessage]
    public class WallState {
        [RabiBroadcast] public readonly List<GameTile> doras;
        [RabiBroadcast] public readonly int remaining;
        [RabiBroadcast] public readonly int rinshanRemaining;

        public WallState(Wall wall) {
            doras = wall.doras.Take(wall.revealedDoraCount).ToList();
            remaining = wall.NumRemaining;
            rinshanRemaining = wall.rinshan.Count;
        }

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
    public class PlayerState : IRabiPlayerMessage {
        public int playerId { get; init; }
        [RabiBroadcast] public readonly int id;
        [RabiBroadcast] public readonly long points;
        [RabiBroadcast] public readonly PlayerHandState hand;

        public PlayerState(Player player, int receiverId) {
            playerId = receiverId;
            id = player.id;
            points = player.points;
            hand = new PlayerHandState(player.hand, receiverId);
        }

        public PlayerStateMsg ToProto(int playerId) {
            return new PlayerStateMsg {
                Id = id,
                Points = points,
                Hand = hand.ToProto(playerId)
            };
        }
    }

    [RabiMessage]
    public class GameState : IRabiPlayerMessage {
        public int playerId { get; init; }

        [RabiBroadcast] public readonly GameConfig config;
        [RabiBroadcast] public readonly GameInfo info;
        [RabiBroadcast] public readonly WallState wall;
        [RabiBroadcast] public readonly PlayerState[] players;

        public GameState(Game game, int playerId) {
            this.playerId = playerId;
            config = game.config;
            info = game.info;
            wall = new WallState(game.wall);
            players = game.players.Select(p => new PlayerState(p, playerId)).ToArray();
        }

        public GameStateMsg ToProto(int playerId) {
            var ret = new GameStateMsg {
                Config = config.ToProto(),
                Info = info.ToProto(),
                Wall = wall.ToProto(),
            };
            ret.Players.AddRange(players.Select(x => x.ToProto(playerId)));
            return ret;
        }
    }
}