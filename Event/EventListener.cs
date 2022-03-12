using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
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
        public EventListener<T> OnStart(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Prepare + PRIORITY_DELTA, times);

        /// <summary> 事件开始时，事件信息已处理完毕 </summary>
        public EventListener<T> Before(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Prepare - PRIORITY_DELTA, times);

        /// <summary> 处理事件前，可以修改事件信息 </summary>
        public EventListener<T> EarlyExec(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Execute + PRIORITY_DELTA, times);

        /// <summary> 处理事件后，一般不要在此取消事件，否则事件不会被广播 </summary>
        public EventListener<T> LateExec(Func<T, Task> handler, int times = -1)
            => ListenTo(handler, EventPriority.Execute - PRIORITY_DELTA, times);

        /// <summary> 广播消息后 </summary>
        public EventListener<T> AfterMsg(Func<T, Task> handler, int times = -1)
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
        public void CancelAll() {
            foreach (var listener in listeners) {
                eventBus.Unregister(listener);
            }
        }

        /// <summary>
        /// 取消一个监听
        /// </summary>
        public void Cancel(Func<T, Task> handler) {
            eventBus.Unregister(handler);
            listeners.Remove(handler);
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