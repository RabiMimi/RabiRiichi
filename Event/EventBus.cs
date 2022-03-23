using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event {

    public class EventBus {
        private interface IEventTrigger {
            /// <summary> Priority of this trigger </summary>
            int Priority { get; }
            Task Trigger(EventBase ev);
            /// <summary> Number of times to trigger. Negative values means infinity. </summary>
            int Times { get; }
            List<IEventTrigger> ParentList { get; }
        }

        private class EventTrigger<T> : IEventTrigger where T : EventBase {
            public int Priority { get; init; }
            public int Times { get; private set; }
            public List<IEventTrigger> ParentList { get; init; }
            public Func<T, Task> trigger { get; init; }
            public EventTrigger(Func<T, Task> handler, int priority, int times, List<IEventTrigger> fromList) {
                Priority = priority;
                trigger = handler;
                Times = times;
                ParentList = fromList;
            }

            public Task Trigger(EventBase ev) {
                if (Times > 0) {
                    Times--;
                }
                return trigger((T)ev);
            }
        }

        private readonly Dictionary<Type, List<IEventTrigger>> listeners =
            new();

        public void Subscribe<T>(Func<T, Task> listener, int priority, int times = -1)
            where T : EventBase {
            var type = typeof(T);
            if (!listeners.TryGetValue(type, out var list)) {
                list = new List<IEventTrigger>();
                listeners.Add(type, list);
            }
            list.Add(new EventTrigger<T>(listener, priority, times, list));
        }

        public void Unsubscribe<T>(Func<T, Task> listener)
            where T : EventBase {
            var type = typeof(T);
            if (!listeners.TryGetValue(type, out var list)) {
                return;
            }
            list.RemoveAll(l => l is EventTrigger<T> et && et.trigger == listener);
        }

        /// <summary>
        /// 处理一个事件
        /// <returns>是否处理成功</returns>
        /// </summary>
        public async Task<bool> Process(EventBase ev) {
            if (ev == null || ev.IsFinishedOrCancelled) {
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
                if (listener.Times == 0) {
                    listener.ParentList.Remove(listener);
                }
                if (ev.IsCancelled) {
                    return false;
                }
                ev.phase = listener.Priority;
            }

            ev.Finish();

            return true;
        }
    }
}
