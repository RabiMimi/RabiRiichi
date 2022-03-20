using RabiRiichi.Communication;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace RabiRiichi.Event {
    public static class EventPriority {
        public const int Cancelled = -1;
        public const int Finished = 0;
        public const int Minimum = 1;
        public const int After = (int)1e6;
        public const int Broadcast = (int)2e6;
        public const int Execute = (int)3e6;
        public const int Prepare = (int)4e6;
        public const int Maximum = (int)1e7;
    }

    public abstract class EventBase : IRabiMessage {
        [RabiBroadcast] public int id;
        [RabiBroadcast] public abstract string name { get; }
        [RabiBroadcast] public RabiMessageType msgType => RabiMessageType.Event;

        public readonly Game game;
        public EventBus bus => game.eventBus;
        public int phase = EventPriority.Maximum;

        /// <summary> 是否已经处理完毕或被取消 </summary>
        public bool IsFinishedOrCancelled => phase <= EventPriority.Finished;

        /// <summary> 是否已经处理完毕 </summary>
        public bool IsFinished => phase == EventPriority.Finished;

        /// <summary> 是否被取消 </summary>
        public bool IsCancelled => phase == EventPriority.Cancelled;

        /// <summary> 等待事件被成功处理 </summary>
        private readonly TaskCompletionSource finishTcs = new();
        public Task WaitForFinish => IsFinished ? Task.CompletedTask : finishTcs.Task;

        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public readonly Dictionary<string, object> extraData = new();

        /// <summary> 事件结束后回调 </summary>
        private readonly List<System.Action<EventBase>> finishCallbacks = new();
        /// <summary> 事件被取消后回调 </summary>
        private readonly List<System.Action<EventBase>> cancelCallbacks = new();

        public EventBase(Game game) {
            this.game = game;
            id = game.info.eventId.Next;
        }

        /// <summary> 强制取消该事件 </summary>
        public void Cancel() {
            phase = EventPriority.Cancelled;
            foreach (var callback in cancelCallbacks) {
                callback(this);
            }
            finishTcs.SetCanceled();
        }

        /// <summary> 结束事件 </summary>
        public void Finish() {
            phase = EventPriority.Finished;
            foreach (var callback in finishCallbacks) {
                callback(this);
            }
            finishTcs.SetResult();
        }

        public void OnFinish(Action<EventBase> callback) {
            finishCallbacks.Add(callback);
        }

        public void OnCancel(Action<EventBase> callback) {
            cancelCallbacks.Add(callback);
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
