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

        public EventListener<T> Before(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Prepare - PRIORITY_DELTA);

        public EventListener<T> EarlyExec(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Execute + PRIORITY_DELTA);

        public EventListener<T> LateExec(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.Execute - PRIORITY_DELTA);

        public EventListener<T> After(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.After + PRIORITY_DELTA);
        public EventListener<T> Final(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.After - PRIORITY_DELTA);

        public EventListener<T> FinalMsg(Func<T, Task> handler)
            => ListenTo(handler, EventPriority.MessageSender - 100);

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