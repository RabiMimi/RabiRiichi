using RabiRiichi.Event.InGame;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Event {
    public class EventQueue {
        private readonly Queue<EventBase> queue = new();
        /// <summary>
        /// Mutex lock that will be acquired upon processing events.
        /// <para>When this lock is acquired, no other thread can add events to the queue and the game state is volatile.</para>
        /// </summary>
        public readonly SemaphoreSlim eventProcessingLock = new(1, 1);
        private const int EVENT_PROCESSING_TIMEOUT = 60 * 60 * 1000;

        public readonly EventBus bus;
        public readonly bool stopOnEmptyQueue;

        public EventQueue(EventBus bus, bool stopOnEmptyQueue = true) {
            this.bus = bus;
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
                using var sh = new SemaphoreHolder(eventProcessingLock, EVENT_PROCESSING_TIMEOUT);
                if (await bus.Process(ev)) {
                    if (ev is TerminateEvent) {
                        break;
                    }
                }
            }
        }
    }
}