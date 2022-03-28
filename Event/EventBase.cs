using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
    public static class EventPriority {
        public const int Cancelled = -1;
        public const int Finished = 0;
        public const int Minimum = 1;
        public const int After = (int)1e6;
        public const int Broadcast = (int)2e6;
        public const int Execute = (int)3e6;
        public const int Prepare = (int)4e6;
        public const int Maximum = (int)1e7;
    }

    public abstract class EventBase : IRabiMessage {
        /// <summary> 广播时的ID，保证递增 </summary>
        [RabiBroadcast] public int id;
        [RabiBroadcast] public abstract string name { get; }
        [RabiBroadcast] public RabiMessageType msgType => RabiMessageType.Event;

        /// <summary> 游戏实例 </summary>
        public readonly Game game;
        /// <summary> 事件总线 </summary>
        public EventBus bus => game.eventBus;
        /// <summary> 该事件所在的队列 </summary>
        public EventQueue Q;
        /// <summary> 事件状态 </summary>
        public int phase = EventPriority.Maximum;

        /// <summary> 是否已经处理完毕或被取消 </summary>
        public bool IsFinishedOrCancelled => phase <= EventPriority.Finished;

        /// <summary> 是否已经处理完毕 </summary>
        public bool IsFinished => phase == EventPriority.Finished;

        /// <summary> 是否被取消 </summary>
        public bool IsCancelled => phase == EventPriority.Cancelled;
        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public readonly Dictionary<string, object> extraData = new();

        /// <summary> 该事件的父事件 </summary>
        public readonly EventBase parent;

        /// <summary> 该事件的子事件 </summary>
        public readonly List<EventBase> children = new();
        /// <summary> 结束时触发事件 </summary>
        private System.Action onFinish;

        public EventBase(EventBase parent) {
            if (parent != null) {
                this.parent = parent;
                this.game = parent.game;
                this.parent.children.Add(this);
                this.Q = parent.Q;
            }
        }

        public EventBase(Game game) {
            this.game = game;
        }

        /// <summary> 事件广播时再初始化唯一事件ID </summary>
        internal void BeforeBroadcast() {
            id = game.info.eventId.Next;
        }

        /// <summary> 强制取消该事件及其后继事件 </summary>
        public void Cancel() {
            if (IsCancelled) {
                return;
            }
            phase = EventPriority.Cancelled;
            foreach (var child in children) {
                child.Cancel();
            }
        }

        /// <summary> 结束事件 </summary>
        public void Finish() {
            if (IsFinishedOrCancelled) {
                return;
            }
            phase = EventPriority.Finished;
            onFinish?.Invoke();
        }

        /// <summary> 在事件成功处理后执行回调 </summary>
        public void OnFinish(System.Action callback) {
            onFinish += callback;
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
