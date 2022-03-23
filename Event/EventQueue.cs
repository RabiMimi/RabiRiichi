using RabiRiichi.Event.InGame;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RabiRiichi.Event {
    public class EventQueue {
        private readonly Queue<EventBase> queue = new();
        public readonly EventBus bus;
        public readonly bool shouldLock;
        public readonly bool stopOnEmptyQueue;

        public EventQueue(EventBus bus, bool shouldLock = false, bool stopOnEmptyQueue = true) {
            this.bus = bus;
            this.shouldLock = shouldLock;
            this.stopOnEmptyQueue = stopOnEmptyQueue;
        }

        public void Queue(EventBase ev) {
            lock (queue) {
                queue.Enqueue(ev);
            }
            ev.Q = this;
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
                    if (!queue.TryPeek(out ev)) {
                        if (stopOnEmptyQueue) {
                            break;
                        }
                        continue;
                    }
                }
                if (await bus.Process(ev, shouldLock)) {
                    if (ev is TerminateEvent) {
                        break;
                    }
                }
            }
        }
    }
}