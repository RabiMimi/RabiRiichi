using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Event {
    public static class Priority {
        public const int Cancelled = -1;
        public const int Finished = 0;
        public const int Minimum = 1;
        public const int MessageSender = (int)1e3;
        public const int After = (int)1e6;
        public const int Execute = (int)2e6;
        public const int Prepare = (int)3e6;
        public const int Maximum = (int)1e7;
    }

    public abstract class EventBase {
        public Game game;
        public int phase = Priority.Maximum;

        /// <summary> 是否已经处理完毕或被取消 </summary>
        public bool IsFinished => phase <= Priority.Finished;

        /// <summary> 是否被取消 </summary>
        public bool IsCancelled => phase == Priority.Cancelled;

        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public Dictionary<string, object> extraData = new Dictionary<string, object>();

        public EventBase(Game game) {
            this.game = game;
        }

        /// <summary> 强制取消该事件 </summary>
        public void Cancel() {
            phase = Priority.Cancelled;
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
