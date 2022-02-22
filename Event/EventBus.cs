using RabiRiichi.Event.Listener;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RabiRiichi.Event {

    public class EventBus {
        private class EventListener {
            public int priority;
            public Func<EventBase, Task> listener;
            public EventListener(Func<EventBase, Task> listener, int priority) {
                this.priority = priority;
                this.listener = listener;
            }
        }

        private readonly Game game;

        public EventBus(Game game) {
            this.game = game;
        }

        private readonly Dictionary<Type, List<EventListener>> listeners =
            new Dictionary<Type, List<EventListener>>();

        private readonly BlockingCollection<EventBase> queue = new BlockingCollection<EventBase>();
        public int Count => queue.Count;
        public bool Empty => Count == 0;

        public void Register<T>(Func<EventBase, Task> listener, int priority)
            where T : EventBase {
            var type = typeof(T);
            if (!listeners.TryGetValue(type, out var list)) {
                list = new List<EventListener>();
                listeners.Add(type, list);
            }
            list.Add(new EventListener(listener, priority));
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
            var list = new List<EventListener>();
            foreach (var (key, value) in listeners) {
                if (key.IsAssignableFrom(ev.GetType())) {
                    list.AddRange(value);
                }
            }

            // 按priority从大到小排序
            list.Sort((a, b) => b.priority.CompareTo(a.priority));

            foreach (var listener in list) {
                await listener.listener(ev);
                if (ev.IsCancelled) {
                    return false;
                }
                ev.phase = listener.priority;
            }

            ev.phase = Priority.Finished;

            return true;
        }
    }
}
