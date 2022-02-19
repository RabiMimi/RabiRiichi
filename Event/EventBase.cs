using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Event {
    public static class Priority {
        public const uint Finished = 0;
        public const uint Minimum = 1;
        public const uint MessageSender = (uint)1e3;
        public const uint After = (uint)1e6;
        public const uint Execute = (uint)2e6;
        public const uint Prepare = (uint)3e6;
        public const uint Maximum = (uint)1e7;
    }

    public abstract class EventBase {
        public Game game;
        public uint phase = Priority.Maximum;

        public bool IsFinished => phase == Priority.Finished;

        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public Dictionary<string, object> extraData = new Dictionary<string, object>();

        public EventBase(Game game) {
            this.game = game;
        }

        /// <summary> 强制取消该事件 </summary>
        public void Cancel() {
            phase = Priority.Finished;
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
