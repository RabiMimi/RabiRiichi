using RabiRiichi.Event.InGame;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event {

    public class EventBus {
        private interface IEventTrigger {
            int Priority { get; }
            Task Trigger(EventBase ev);
        }

        private class EventTrigger<T> : IEventTrigger where T : EventBase {
            public int Priority { get; private set; }
            public Func<T, Task> trigger;
            public EventTrigger(Func<T, Task> handler, int priority) {
                Priority = priority;
                trigger = handler;
            }

            public Task Trigger(EventBase ev) {
                return trigger((T)ev);
            }
        }

        private readonly Dictionary<Type, List<IEventTrigger>> listeners =
            new();

        private readonly BlockingCollection<EventBase> queue = new();
        public int Count => queue.Count;
        public bool Empty => Count == 0;

        public void Register<T>(Func<T, Task> listener, int priority)
            where T : EventBase {
            var type = typeof(T);
            if (!listeners.TryGetValue(type, out var list)) {
                list = new List<IEventTrigger>();
                listeners.Add(type, list);
            }
            list.Add(new EventTrigger<T>(listener, priority));
        }

        public void Unregister<T>(Func<T, Task> listener)
            where T : EventBase {
            var type = typeof(T);
            if (!listeners.TryGetValue(type, out var list)) {
                return;
            }
            list.RemoveAll(l => (l is EventTrigger<T> et) && et.trigger == listener);
        }

        public void Queue(EventBase ev) {
            queue.Add(ev);
        }

        /// <summary> 开始处理事件队列 </summary>
        public async Task ProcessQueue() {
            while (true) {
                var ev = queue.Take();
                if (await Process(ev)) {
                    if (ev is StopGameEvent) {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 直接处理一个事件
        /// <returns>是否处理成功</returns>
        /// </summary>
        public async Task<bool> Process(EventBase ev) {
            if (ev == null || ev.IsFinished) {
                return false;
            }

            // 监听该事件及所有子类
            var list = new List<IEventTrigger>();
            foreach (var (key, value) in listeners) {
                if (key.IsAssignableFrom(ev.GetType())) {
                    list.AddRange(value);
                }
            }

            // 按priority从大到小排序
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            foreach (var listener in list) {
                await listener.Trigger(ev);
                if (ev.IsCancelled) {
                    return false;
                }
                ev.phase = listener.Priority;
            }

            ev.phase = EventPriority.Finished;

            return true;
        }
    }
}
