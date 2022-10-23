using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Utils.Graphs;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Communication.Proto {
    public class ProtoConverters {
        #region Key Constants
        public const string PLAYER_ID = "pid";
        #endregion

        #region ActionMsg
        [Produces]
        public static PlayerActionMsg ConvertAgariActionMsg([Consumes] AgariActionMsg action)
            => new() { AgariAction = action };

        [Produces]
        public static PlayerActionMsg ConvertChiiActionMsg([Consumes] ChiiActionMsg action)
            => new() { ChiiAction = action };

        [Produces]
        public static PlayerActionMsg ConvertPonActionMsg([Consumes] PonActionMsg action)
            => new() { PonAction = action };

        [Produces]
        public static PlayerActionMsg ConvertKanActionMsg([Consumes] KanActionMsg action)
            => new() { KanAction = action };

        [Produces]
        public static PlayerActionMsg ConvertRiichiActionMsg([Consumes] RiichiActionMsg action)
            => new() { RiichiAction = action };

        [Produces]
        public static PlayerActionMsg ConvertRyuukyokuActionMsg([Consumes] RyuukyokuActionMsg action)
            => new() { RyuukyokuAction = action };

        [Produces]
        public static PlayerActionMsg ConvertPlayTileActionMsg([Consumes] PlayTileActionMsg action)
            => new() { PlayTileAction = action };

        [Produces]
        public static PlayerActionMsg ConvertSkipActionMsg([Consumes] SkipActionMsg action)
            => new() { SkipAction = action };
        #endregion

        #region EventMsg
        [Produces]
        public static EventMsg ConvertAddKanEventMsg([Consumes] AddKanEventMsg ev)
            => new() { AddKanEvent = ev };

        [Produces]
        public static EventMsg ConvertAddTileEventMsg([Consumes] AddTileEventMsg ev)
            => new() { AddTileEvent = ev };

        [Produces]
        public static EventMsg ConvertAgariEventMsg([Consumes] AgariEventMsg ev)
            => new() { AgariEvent = ev };

        [Produces]
        public static EventMsg ConvertApplyScoreEventMsg([Consumes] ApplyScoreEventMsg ev)
            => new() { ApplyScoreEvent = ev };

        [Produces]
        public static EventMsg ConvertBeginGameEventMsg([Consumes] BeginGameEventMsg ev)
            => new() { BeginGameEvent = ev };

        [Produces]
        public static EventMsg ConvertCalcScoreEventMsg([Consumes] CalcScoreEventMsg ev)
            => new() { CalcScoreEvent = ev };

        [Produces]
        public static EventMsg ConvertClaimTileEventMsg([Consumes] ClaimTileEventMsg ev)
            => new() { ClaimTileEvent = ev };

        [Produces]
        public static EventMsg ConvertConcludeGameEventMsg([Consumes] ConcludeGameEventMsg ev)
            => new() { ConcludeGameEvent = ev };

        [Produces]
        public static EventMsg ConvertDealerFirstTurnEventMsg([Consumes] DealerFirstTurnEventMsg ev)
            => new() { DealerFirstTurnEvent = ev };

        [Produces]
        public static EventMsg ConvertDealHandEventMsg([Consumes] DealHandEventMsg ev)
            => new() { DealHandEvent = ev };

        [Produces]
        public static EventMsg ConvertDiscardTileEventMsg([Consumes] DiscardTileEventMsg ev)
            => new() { DiscardTileEvent = ev };

        [Produces]
        public static EventMsg ConvertDrawTileEventMsg([Consumes] DrawTileEventMsg ev)
            => new() { DrawTileEvent = ev };

        [Produces]
        public static EventMsg ConvertIncreaseJunEventMsg([Consumes] IncreaseJunEventMsg ev)
            => new() { IncreaseJunEvent = ev };

        [Produces]
        public static EventMsg ConvertKanEventMsg([Consumes] KanEventMsg ev)
            => new() { KanEvent = ev };

        [Produces]
        public static EventMsg ConvertLateClaimTileEventMsg([Consumes] LateClaimTileEventMsg ev)
            => new() { LateClaimTileEvent = ev };

        [Produces]
        public static EventMsg ConvertNextGameEventMsg([Consumes] NextGameEventMsg ev)
            => new() { NextGameEvent = ev };

        [Produces]
        public static EventMsg ConvertNextPlayerEventMsg([Consumes] NextPlayerEventMsg ev)
            => new() { NextPlayerEvent = ev };

        [Produces]
        public static EventMsg ConvertRevealDoraEventMsg([Consumes] RevealDoraEventMsg ev)
            => new() { RevealDoraEvent = ev };

        [Produces]
        public static EventMsg ConvertRyuukyokuEventMsg([Consumes] RyuukyokuEventMsg ev)
            => new() { RyuukyokuEvent = ev };

        [Produces]
        public static EventMsg ConvertSetFuritenEventMsg([Consumes] SetFuritenEventMsg ev)
            => new() { SetFuritenEvent = ev };

        [Produces]
        public static EventMsg ConvertSetIppatsuEventMsg([Consumes] SetIppatsuEventMsg ev)
            => new() { SetIppatsuEvent = ev };

        [Produces]
        public static EventMsg ConvertSetMenzenEventMsg([Consumes] SetMenzenEventMsg ev)
            => new() { SetMenzenEvent = ev };

        [Produces]
        public static EventMsg ConvertSetRiichiEventMsg([Consumes] SetRiichiEventMsg ev)
            => new() { SetRiichiEvent = ev };

        [Produces]
        public static EventMsg ConvertStopGameEventMsg([Consumes] StopGameEventMsg ev)
            => new() { StopGameEvent = ev };

        [Produces]
        public static EventMsg ConvertSyncGameStateEventMsg([Consumes] SyncGameStateEventMsg ev)
            => new() { SyncGameStateEvent = ev };
        #endregion

        #region Action
        private static IEnumerable<MenLikeMsg> ConvertTilesOptions(IEnumerable<ActionOption> options)
            => options.Select(o => MenLike.From(((ChooseTilesActionOption)o).tiles).ToProto());

        private static IEnumerable<GameTileMsg> ConvertTileOptions(IEnumerable<ActionOption> options)
            => options.Select(o => ConvertGameTileBroadcast(((ChooseTileActionOption)o).tile));

        [Produces]
        public static AgariActionMsg ConvertAgariAction([Consumes] AgariAction action) {
            var ret = new AgariActionMsg();
            if (action is RonAction) {
                ret.Type = AgariType.Ron;
            } else if (action is TsumoAction) {
                ret.Type = AgariType.Tsumo;
            }
            return ret;
        }

        [Produces]
        public static ChiiActionMsg ConvertChiiAction([Consumes] ChiiAction action) {
            var ret = new ChiiActionMsg();
            ret.TileGroups.AddRange(ConvertTilesOptions(action.options));
            return ret;
        }

        [Produces]
        public static PonActionMsg ConvertPonAction([Consumes] PonAction action) {
            var ret = new PonActionMsg();
            ret.TileGroups.AddRange(ConvertTilesOptions(action.options));
            return ret;
        }

        [Produces]
        public static KanActionMsg ConvertKanAction([Consumes] KanAction action) {
            var ret = new KanActionMsg();
            ret.TileGroups.AddRange(ConvertTilesOptions(action.options));
            return ret;
        }

        [Produces]
        public static RiichiActionMsg ConvertRiichiAction([Consumes] RiichiAction action) {
            var ret = new RiichiActionMsg();
            ret.Tiles.AddRange(ConvertTileOptions(action.options));
            return ret;
        }

        [Produces]
        public static RyuukyokuActionMsg ConvertRyuukyokuAction([Consumes] RyuukyokuAction action)
            => new() { Reason = action.reason };

        [Produces]
        public static PlayTileActionMsg ConvertSkipAction([Consumes] PlayTileAction action) {
            var ret = new PlayTileActionMsg();
            ret.Tiles.AddRange(ConvertTileOptions(action.options));
            return ret;
        }

        [Produces]
        public static SkipActionMsg ConvertSkipAction([Consumes] SkipAction _)
            => new();
        #endregion

        #region Event
        [Produces]
        public static AddKanEventMsg ConvertAddKanEvent([Consumes] AddKanEvent ev, [Consumes(PLAYER_ID)] int playerId)
            => new() {
                PlayerId = ev.playerId,
                Kan = ev.kan.ToProto(),
                Incoming = ConvertGameTile(ev.incoming, playerId),
                KanSource = ev.kanSource,
            };

        [Produces]
        public static AddTileEventMsg ConvertAddTileEvent([Consumes] AddTileEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new AddTileEventMsg {
                PlayerId = ev.playerId,
            };
            ret.Incoming = ConvertGameTile(ev.incoming, playerId);
            return ret;
        }

        [Produces]
        public static AgariInfoListMsg ConvertAgariInfoList([Consumes] AgariInfoList infos) {
            var ret = new AgariInfoListMsg {
                FromPlayer = infos.fromPlayer,
                Incoming = ConvertGameTileBroadcast(infos.incoming),
            };
            ret.AgariInfos.Add(infos.Select(x => x.ToProto()));
            return ret;
        }

        [Produces]
        public static AgariEventMsg ConvertAgariEvent([Consumes] AgariEvent ev)
            => new() {
                IsTsumo = ev.isTsumo,
                AgariInfos = ConvertAgariInfoList(ev.agariInfos),
            };

        [Produces]
        public static ApplyScoreEventMsg ConvertApplyScoreEvent([Consumes] ApplyScoreEvent ev) {
            var ret = new ApplyScoreEventMsg();
            ret.ScoreChange.AddRange(ev.scoreChange.Select(x => x.ToProto()));
            return ret;
        }

        [Produces]
        public static BeginGameEventMsg ConvertBeginGameEvent([Consumes] BeginGameEvent ev)
            => new() {
                Round = ev.round,
                Dealer = ev.dealer,
                Honba = ev.honba,
            };

        [Produces]
        public static CalcScoreEventMsg ConvertCalcScoreEvent([Consumes] CalcScoreEvent ev) {
            var ret = new CalcScoreEventMsg {
                AgariInfos = ConvertAgariInfoList(ev.agariInfos),
            };
            ret.ScoreChange.AddRange(ev.scoreChange.Select(x => x.ToProto()));
            return ret;
        }

        [Produces]
        public static ClaimTileEventMsg ConvertClaimTileEvent([Consumes] ClaimTileEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Tile = ConvertGameTileBroadcast(ev.tile),
                Group = ev.group.ToProto(),
                Reason = ev.reason,
            };

        [Produces]
        public static ConcludeGameEventMsg ConvertConcludeGameEvent([Consumes] ConcludeGameEvent ev)
            => new() {
                Doras = ev.doras.ToString(),
                Uradoras = ev.uradoras.ToString(),
            };

        [Produces]
        public static DealerFirstTurnEventMsg ConvertDealerFirstTurnEvent(
            [Consumes] DealerFirstTurnEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new DealerFirstTurnEventMsg {
                PlayerId = ev.playerId,
                Incoming = ConvertGameTile(ev.incoming, playerId)
            };
            return ret;
        }

        [Produces]
        public static DealHandEventMsg ConvertDealHandEvent([Consumes] DealHandEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new DealHandEventMsg {
                PlayerId = ev.playerId,
                Count = ev.count,
            };
            ret.Tiles.AddRange(ev.tiles.Select(tile => ConvertGameTile(tile, playerId)));
            return ret;
        }

        [Produces]
        public static DiscardTileEventMsg ConvertDiscardTileEvent(
            [Consumes] DiscardTileEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new DiscardTileEventMsg {
                PlayerId = ev.playerId,
                Discarded = ConvertGameTileBroadcast(ev.discarded),
                Reason = ev.reason,
                FromHand = ev.fromHand,
            };
            ret.Incoming = ConvertGameTile(ev.incoming, playerId);
            ret.IsRiichi = ev is RiichiEvent;
            return ret;
        }

        [Produces]
        public static DrawTileEventMsg ConvertDrawTileEvent(
            [Consumes] DrawTileEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new DrawTileEventMsg {
                PlayerId = ev.playerId,
                Source = ev.source,
            };
            ret.Tile = ConvertGameTile(ev.tile, playerId);
            return ret;
        }

        [Produces]
        public static IncreaseJunEventMsg ConvertIncreaseJunEvent([Consumes] IncreaseJunEvent ev)
            => new() {
                PlayerId = ev.playerId,
                IncreasedJun = ev.increasedJun,
            };

        [Produces]
        public static KanEventMsg ConvertKanEvent([Consumes] KanEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Kan = ev.kan.ToProto(),
                KanSource = ev.kanSource,
            };

        [Produces]
        public static LateClaimTileEventMsg ConvertLateClaimTileEvent([Consumes] LateClaimTileEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Reason = ev.reason,
            };

        [Produces]
        public static NextGameEventMsg ConvertNextGameEvent([Consumes] NextGameEvent ev)
            => new() {
                NextRound = ev.nextRound,
                NextDealer = ev.nextDealer,
                NextHonba = ev.nextHonba,
                RiichiStick = ev.riichiStick,
            };

        [Produces]
        public static NextPlayerEventMsg ConvertNextPlayerEvent([Consumes] NextPlayerEvent ev)
            => new() {
                PlayerId = ev.playerId,
                NextPlayerId = ev.nextPlayerId,
            };

        [Produces]
        public static RevealDoraEventMsg ConvertRevealDoraEvent([Consumes] RevealDoraEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Dora = ConvertGameTileBroadcast(ev.dora),
            };

        [Produces]
        public static RyuukyokuEventMsg ConvertRyuukyokuEvent([Consumes] RyuukyokuEvent ev) {
            var ret = new RyuukyokuEventMsg();
            ret.ScoreChange.AddRange(ev.scoreChange.Select(x => x.ToProto()));
            if (ev is EndGameRyuukyokuEvent ege) {
                ret.EndGameRyuukyoku = new EndGameRyuukyokuEventMsg();
                ret.EndGameRyuukyoku.RemainingPlayers.AddRange(ege.remainingPlayers);
                ret.EndGameRyuukyoku.NagashiManganPlayers.AddRange(ege.nagashiManganPlayers);
                ret.EndGameRyuukyoku.TenpaiPlayers.AddRange(ege.tenpaiPlayers);
            } else if (ev is MidGameRyuukyokuEvent mge) {
                ret.MidGameRyuukyoku = new MidGameRyuukyokuEventMsg {
                    Name = mge.name,
                };
            }
            return ret;
        }

        [Produces]
        public static SetFuritenEventMsg ConvertSetFuritenEvent([Consumes] SetFuritenEvent ev) {
            return new() {
                PlayerId = ev.playerId,
                Furiten = ev.furiten,
                FuritenType = ev switch {
                    SetTempFuritenEvent => FuritenType.Temp,
                    SetRiichiFuritenEvent => FuritenType.Riichi,
                    SetDiscardFuritenEvent => FuritenType.Discard,
                    _ => FuritenType.None,
                }
            };
        }

        [Produces]
        public static SetIppatsuEventMsg ConvertSetIppatsuEvent([Consumes] SetIppatsuEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Ippatsu = ev.ippatsu,
            };

        [Produces]
        public static SetMenzenEventMsg ConvertSetMenzenEvent([Consumes] SetMenzenEvent ev)
            => new() {
                PlayerId = ev.playerId,
                Menzen = ev.menzen,
            };

        [Produces]
        public static SetRiichiEventMsg ConvertSetRiichiEvent([Consumes] SetRiichiEvent ev)
            => new() {
                PlayerId = ev.playerId,
                RiichiTile = ConvertGameTileBroadcast(ev.riichiTile),
                WRiichi = ev.wRiichi,
            };

        [Produces]
        public static StopGameEventMsg ConvertStopGameEvent([Consumes] StopGameEvent ev) {
            var ret = new StopGameEventMsg();
            ret.EndGamePoints.AddRange(ev.endGamePoints);
            return ret;
        }

        [Produces]
        public static SyncGameStateEventMsg ConvertSyncGameStateEvent([Consumes] SyncGameStateEvent ev, [Consumes(PLAYER_ID)] int playerId) {
            var ret = new SyncGameStateEventMsg {
                PlayerId = ev.playerId,
                GameState = ev.gameState?.ToProto(playerId),
            };
            if (ev.playerId == playerId) {
                foreach (var (key, value) in ev.extra) {
                    ret.Extra.Add(key, value);
                }
            }
            return ret;
        }
        #endregion

        #region Inquiry
        [Produces]
        public static SinglePlayerInquiryMsg ConvertInquiry([Consumes] ProducerGraph graph, [Consumes] SinglePlayerInquiry inq) {
            var ret = new SinglePlayerInquiryMsg();
            ret.Actions.AddRange(inq.actions.Select(
                action => graph.Build().SetInput(action).Execute<PlayerActionMsg>()));
            return ret;
        }
        #endregion

        #region Core
        [Produces]
        public static DiscardInfoMsg ConvertDiscardInfo([Consumes] DiscardInfo info)
            => info == null ? null : new() {
                From = info.from,
                Reason = info.reason,
                Time = info.time,
            };

        [Produces]
        public static GameTileMsg ConvertGameTile([Consumes] GameTile tile, [Consumes(PLAYER_ID)] int playerId)
            => tile == null ? null : new() {
                TraceId = tile.traceId,
                Tile = tile.playerId == playerId ? tile.tile.Val : Tile.Empty,
                PlayerId = tile.playerId,
                FormTime = tile.formTime,
                Source = tile.source,
                DiscardInfo = ConvertDiscardInfo(tile.discardInfo),
            };

        public static GameTileMsg ConvertGameTileBroadcast(GameTile tile)
            => tile == null ? null : ConvertGameTile(tile, tile.playerId);
        #endregion
    }
}