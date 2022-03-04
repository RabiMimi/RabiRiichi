using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
    /// <summary>
    /// 自定义事件监听器，一般临时使用
    /// </summary>
    public class EventListener<T> where T : EventBase {
        private readonly Game game;
        private readonly EventBus eventBus;
        private readonly List<Func<T, Task>> listeners = new List<Func<T, Task>>();
        private const int PRIORITY_DELTA = 1000;

        public EventListener(Game game, EventBus eventBus) {
            this.game = game;
            this.eventBus = eventBus;
        }

        public EventListener<T> ListenTo(Func<T, Task> handler, int priority) {
            eventBus.Register<T>(handler, priority);
            listeners.Add(handler);
            return this;
        }

        /// <summary> 事件开始前，事件信息可能未准备好 </summary>
        public EventListener<T> OnStart(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Prepare + PRIORITY_DELTA);

        /// <summary> 事件开始时，事件信息已处理完毕 </summary>
        public EventListener<T> Before(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Prepare - PRIORITY_DELTA);

        /// <summary> 处理事件前，可以修改事件信息 </summary>
        public EventListener<T> EarlyExec(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Execute + PRIORITY_DELTA);

        /// <summary> 处理事件后，一般不要在此取消事件，否则事件不会被广播 </summary>
        public EventListener<T> LateExec(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Execute - PRIORITY_DELTA);

        /// <summary> 广播消息后 </summary>
        public EventListener<T> AfterMsg(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Broadcast - PRIORITY_DELTA);

        /// <summary> 事件结束时 </summary>
        public EventListener<T> After(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.After + PRIORITY_DELTA);

        /// <summary> 最晚的时间点 </summary>
        public EventListener<T> Final(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.After - PRIORITY_DELTA);

        /// <summary>
        /// 取消所有监听
        /// </summary>
        public void CancelAll() {
            foreach (var listener in listeners) {
                eventBus.Unregister<T>(listener);
            }
        }

        /// <summary>
        /// 取消一个监听
        /// </summary>
        public void Cancel(Func<T, Task> handler) {
            eventBus.Unregister<T>(handler);
            listeners.Remove(handler);
        }
    }

    /// <summary>
    /// 用于创建临时使用的自定义事件监听器
    /// </summary>
    public class EventListenerFactory {
        private readonly Game game;
        private readonly EventBus eventBus;

        public EventListenerFactory(Game game, EventBus eventBus) {
            this.game = game;
            this.eventBus = eventBus;
        }

        public EventListener<T> Create<T>() where T : EventBase {
            return new EventListener<T>(game, eventBus);
        }
    }
}