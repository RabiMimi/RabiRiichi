using RabiRiichi.Communication;
using RabiRiichi.Core;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Events {
    public static class EventPriority {
        public const int STEP = 1000;
        public const int Cancelled = -1;
        public const int Finished = 0;
        public const int Minimum = 1;
        public const int After = (int)1e6;
        public const int Broadcast = (int)2e6;
        public const int Execute = (int)3e6;
        public const int Prepare = (int)4e6;
        public const int Maximum = (int)1e7;
    }

    [RabiMessage]
    public abstract class EventBase {
        [RabiBroadcast] public abstract string name { get; }

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
        /// <summary> 在事件成功处理后执行回调 </summary>
        public Action OnFinish;

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

        /// <summary> 强制取消该事件及其后继事件 </summary>
        public void Cancel() {
            if (IsFinishedOrCancelled) {
                return;
            }
            phase = EventPriority.Cancelled;
            foreach (EventBase child in children) {
                child.Cancel();
            }
        }

        /// <summary> 按事件类型获取父事件（包含本事件） </summary>
        public T GetInParent<T>() where T : EventBase {
            if (this is T target) {
                return target;
            }
            return parent?.GetInParent<T>();
        }

        /// <summary> 跳过Execute阶段 </summary>
        public void SkipExecution() {
            phase = Math.Min(phase, (EventPriority.Execute + EventPriority.Broadcast) >> 1);
        }

        /// <summary> 结束事件 </summary>
        public void Finish() {
            if (IsFinishedOrCancelled) {
                return;
            }
            phase = EventPriority.Finished;
            OnFinish?.Invoke();
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
