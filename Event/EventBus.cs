using RabiRiichi.Event.Listener;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RabiRiichi.Event {

    public class EventBus {
        private class EventListener {
            public uint priority;
            public Func<EventBase, Task> listener;
            public EventListener(Func<EventBase, Task> listener, uint priority) {
                this.priority = priority;
                this.listener = listener;
            }
        }

        private Game game;

        public EventBus(Game game) {
            this.game = game;
        }

        private readonly Dictionary<Type, List<EventListener>> listeners =
            new Dictionary<Type, List<EventListener>>();

        private readonly BlockingCollection<EventBase> queue = new BlockingCollection<EventBase>();
        public int Count => queue.Count;
        public bool Empty => Count == 0;

        public void Register<T>(Func<EventBase, Task> listener, uint priority)
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

        public async Task ProcessQueue() {
            while (true) {
                var ev = queue.Take();
                await Process(ev);
            }
        }

        public async Task Process(EventBase ev) {
            if (ev == null || ev.IsFinished) {
                return;
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
                if (ev.IsFinished) {
                    break;
                }
                ev.phase = listener.priority;
            }
        }

        public void RegisterGameEvents() {
            DefaultDealHand.Register(this);
            DefaultDrawTile.Register(this);
        }
    }
}
