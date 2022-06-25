using RabiRiichi.Communication;
using RabiRiichi.Generated.Events.InGame;

namespace RabiRiichi.Events.InGame {
    public abstract class SetFuritenEvent : PrivatePlayerEvent {
        #region Request
        [RabiBroadcast] public bool furiten;
        #endregion

        public SetFuritenEvent(EventBase parent, int playerId, bool furiten) : base(parent, playerId) {
            this.furiten = furiten;
        }

        public virtual SetFuritenEventMsg ToProto() {
            return new SetFuritenEventMsg {
                PlayerId = playerId,
                Furiten = furiten,
            };
        }
    }

    public class SetTempFuritenEvent : SetFuritenEvent {
        public override string name => "set_temp_furiten";

        public SetTempFuritenEvent(EventBase parent, int playerId, bool furiten) : base(parent, playerId, furiten) { }

        public override SetFuritenEventMsg ToProto() {
            var ret = base.ToProto();
            ret.FuritenType = FuritenType.Temp;
            return ret;
        }
    }

    public class SetRiichiFuritenEvent : SetFuritenEvent {
        public override string name => "set_riichi_furiten";

        public SetRiichiFuritenEvent(EventBase parent, int playerId, bool furiten) : base(parent, playerId, furiten) { }

        public override SetFuritenEventMsg ToProto() {
            var ret = base.ToProto();
            ret.FuritenType = FuritenType.Riichi;
            return ret;
        }
    }

    public class SetDiscardFuritenEvent : SetFuritenEvent {
        public override string name => "set_discard_furiten";

        public SetDiscardFuritenEvent(EventBase parent, int playerId, bool furiten) : base(parent, playerId, furiten) { }

        public override SetFuritenEventMsg ToProto() {
            var ret = base.ToProto();
            ret.FuritenType = FuritenType.Discard;
            return ret;
        }
    }
}