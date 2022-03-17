using RabiRiichi.Event.InGame;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
    public enum EventScope {
        /// <summary> 成功处理下一个事件后移除监听，无论该事件能否触发监听器 </summary>
        Event,
        /// <summary> 本局结束后移除监听 </summary>
        Game
    }

    /// <summary>
    /// 自定义事件监听器，一般临时使用
    /// </summary>
    public class EventListener<T> where T : EventBase {
        private readonly EventBus eventBus;
        private readonly List<Func<T, Task>> listeners = new();
        private const int PRIORITY_DELTA = 1000;

        public EventListener(EventBus eventBus) {
            this.eventBus = eventBus;
        }

        public EventListener<T> ListenTo(Func<T, Task> handler, int priority, int times = -1) {
            eventBus.Register(handler, priority, times);
            listeners.Add(handler);
            return this;
        }

        /// <summary> 事件开始前，事件信息可能未准备好 </summary>
        public EventListener<T> EarlyPrepare(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Prepare + PRIORITY_DELTA, times);

        /// <summary> 事件开始时，事件信息已处理完毕 </summary>
        public EventListener<T> LatePrepare(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Prepare - PRIORITY_DELTA, times);

        /// <summary> 处理事件前，可以修改事件信息 </summary>
        public EventListener<T> EarlyExec(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Execute + PRIORITY_DELTA, times);

        /// <summary> 处理事件后，一般不要在此取消事件，否则事件不会被广播 </summary>
        public EventListener<T> LateExec(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Execute - PRIORITY_DELTA, times);

        /// <summary> 广播消息后 </summary>
        public EventListener<T> AfterBroadcast(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Broadcast - PRIORITY_DELTA, times);

        /// <summary> 事件结束时 </summary>
        public EventListener<T> After(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.After + PRIORITY_DELTA, times);

        /// <summary> 最晚的时间点 </summary>
        public EventListener<T> Final(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.After - PRIORITY_DELTA, times);

        /// <summary>
        /// 取消所有监听
        /// </summary>
        public void Cancel() {
            foreach (var listener in listeners) {
                eventBus.Unregister(listener);
            }
            listeners.Clear();
        }

        private Task CancelHelper<U>(U _) where U : EventBase {
            Cancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 在某个事件成功触发后，取消所有监听
        /// </summary>
        public EventListener<T> CancelOn<U>() where U : EventBase {
            eventBus.Register<U>(CancelHelper, EventPriority.Minimum + PRIORITY_DELTA, 1);
            return this;
        }

        public EventListener<T> ScopeTo(EventScope scope) {
            switch (scope) {
                case EventScope.Event:
                    CancelOn<EventBase>();
                    break;
                case EventScope.Game:
                    CancelOn<NextGameEvent>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown event scope");
            }
            return this;
        }
    }

    /// <summary>
    /// 用于创建临时使用的自定义事件监听器
    /// </summary>
    public class EventListenerFactory {
        private readonly EventBus eventBus;

        public EventListenerFactory(EventBus eventBus) {
            this.eventBus = eventBus;
        }

        public EventListener<T> Create<T>() where T : EventBase {
            return new EventListener<T>(eventBus);
        }
    }
}