using RabiRiichi.Actions;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Actions;
using RabiRiichi.Generated.Events;

namespace RabiRiichi.Communication.Proto {
    public static class ProtoConverters {
        public static PlayerActionMsg ToProto(IPlayerAction action) {
            var ret = new PlayerActionMsg();
            if (action is AgariAction agariAction) {
                ret.AgariAction = agariAction.ToProto();
            } else if (action is ChiiAction chiiAction) {
                ret.ChiiAction = chiiAction.ToProto();
            } else if (action is PonAction ponAction) {
                ret.PonAction = ponAction.ToProto();
            } else if (action is KanAction kanAction) {
                ret.KanAction = kanAction.ToProto();
            } else if (action is RiichiAction riichiAction) {
                ret.RiichiAction = riichiAction.ToProto();
            } else if (action is RyuukyokuAction ryuukyokuAction) {
                ret.RyuukyokuAction = ryuukyokuAction.ToProto();
            } else if (action is PlayTileAction playTileAction) {
                ret.PlayTileAction = playTileAction.ToProto();
            } else if (action is SkipAction skipAction) {
                ret.SkipAction = skipAction.ToProto();
            } else {
                return null;
            }
            return ret;
        }

        public static EventMsg ToProto(EventBase ev, int playerId) {
            var ret = new EventMsg();
            if (ev is AddKanEvent addKanEvent) {
                ret.AddKanEvent = addKanEvent.ToProto();
            } else if (ev is AddTileEvent addTileEvent) {
                ret.AddTileEvent = addTileEvent.ToProto(playerId);
            } else if (ev is AgariEvent agariEvent) {
                ret.AgariEvent = agariEvent.ToProto();
            } else if (ev is ApplyScoreEvent applyScoreEvent) {
                ret.ApplyScoreEvent = applyScoreEvent.ToProto();
            } else if (ev is BeginGameEvent beginGameEvent) {
                ret.BeginGameEvent = beginGameEvent.ToProto();
            } else if (ev is CalcScoreEvent calcScoreEvent) {
                ret.CalcScoreEvent = calcScoreEvent.ToProto();
            } else if (ev is ClaimTileEvent claimTileEvent) {
                ret.ClaimTileEvent = claimTileEvent.ToProto();
            } else if (ev is ConcludeGameEvent concludeGameEvent) {
                ret.ConcludeGameEvent = concludeGameEvent.ToProto();
            } else if (ev is DealerFirstTurnEvent dealerFirstTurnEvent) {
                ret.DealerFirstTurnEvent = dealerFirstTurnEvent.ToProto(playerId);
            } else if (ev is DealHandEvent dealHandEvent) {
                ret.DealHandEvent = dealHandEvent.ToProto();
            } else if (ev is DiscardTileEvent discardTileEvent) {
                ret.DiscardTileEvent = discardTileEvent.ToProto(playerId);
            } else if (ev is DrawTileEvent drawTileEvent) {
                ret.DrawTileEvent = drawTileEvent.ToProto(playerId);
            } else if (ev is IncreaseJunEvent increaseJunEvent) {
                ret.IncreaseJunEvent = increaseJunEvent.ToProto();
            } else if (ev is KanEvent kanEvent) {
                ret.KanEvent = kanEvent.ToProto();
            } else if (ev is LateClaimTileEvent lateClaimTileEvent) {
                ret.LateClaimTileEvent = lateClaimTileEvent.ToProto();
            } else if (ev is NextGameEvent nextGameEvent) {
                ret.NextGameEvent = nextGameEvent.ToProto();
            } else if (ev is NextPlayerEvent nextPlayerEvent) {
                ret.NextPlayerEvent = nextPlayerEvent.ToProto();
            } else if (ev is RevealDoraEvent revealDoraEvent) {
                ret.RevealDoraEvent = revealDoraEvent.ToProto();
            } else if (ev is RyuukyokuEvent ryuukyokuEvent) {
                ret.RyuukyokuEvent = ryuukyokuEvent.ToProto();
            } else if (ev is SetFuritenEvent setFuritenEvent) {
                ret.SetFuritenEvent = setFuritenEvent.ToProto();
            } else if (ev is SetIppatsuEvent setIppatsuEvent) {
                ret.SetIppatsuEvent = setIppatsuEvent.ToProto();
            } else if (ev is SetMenzenEvent setMenzenEvent) {
                ret.SetMenzenEvent = setMenzenEvent.ToProto();
            } else if (ev is SetRiichiEvent setRiichiEvent) {
                ret.SetRiichiEvent = setRiichiEvent.ToProto();
            } else if (ev is StopGameEvent stopGameEvent) {
                ret.StopGameEvent = stopGameEvent.ToProto();
            } else if (ev is SyncGameStateEvent syncGameStateEvent) {
                ret.SyncGameStateEvent = syncGameStateEvent.ToProto(playerId);
            } else {
                return null;
            }
            return ret;
        }
    }
}