using RabiRiichi.Event.InGame;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event {
    public class EventQueue : IEnumerable<EventBase> {
        private readonly Queue<EventBase> queue = new();
        public readonly EventBus bus;
        public readonly bool shouldLock;

        public EventQueue(EventBus bus, bool shouldLock = false) {
            this.bus = bus;
            this.shouldLock = shouldLock;
        }

        public void Queue(EventBase ev) {
            lock (queue) {
                queue.Enqueue(ev);
            }
            ev.Q = this;
        }

        public void QueueIfNotExist<T>(T ev) where T : EventBase {
            lock (queue) {
                if (!queue.Any(e => e is T)) {
                    queue.Enqueue(ev);
                    ev.Q = this;
                }
            }
        }

        public void ClearEvents() {
            lock (queue) {
                queue.Clear();
            }
        }

        /// <summary> 开始处理事件队列 </summary>
        public async Task ProcessQueue() {
            while (true) {
                await Task.Yield();
                EventBase ev;
                lock (queue) {
                    if (!queue.TryDequeue(out ev)) {
                        break;
                    }
                }
                if (await bus.Process(ev, shouldLock)) {
                    if (ev is TerminateEvent) {
                        break;
                    }
                }
            }
        }

        public IEnumerator<EventBase> GetEnumerator() {
            return queue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return queue.GetEnumerator();
        }
    }
}