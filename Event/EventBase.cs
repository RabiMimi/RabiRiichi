using RabiRiichi.Interact;
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

        public Game game;
        public int phase = EventPriority.Maximum;

        /// <summary> 是否已经处理完毕或被取消 </summary>
        public bool IsFinishedOrCancelled => phase <= EventPriority.Finished;

        /// <summary> 是否已经处理完毕 </summary>
        public bool IsFinished => phase == EventPriority.Finished;

        /// <summary> 是否被取消 </summary>
        public bool IsCancelled => phase == EventPriority.Cancelled;

        /// <summary> 等待事件被成功处理 </summary>
        private readonly Lazy<TaskCompletionSource> finishTcs = new();
        public Task WaitForFinish => IsFinished ? Task.CompletedTask : finishTcs.Value.Task;

        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public Dictionary<string, object> extraData = new();

        public EventBase(Game game) {
            this.game = game;
            id = game.info.eventId.Next;
        }

        /// <summary> 强制取消该事件 </summary>
        public void Cancel() {
            phase = EventPriority.Cancelled;
        }

        public void Finish() {
            phase = EventPriority.Finished;
            if (finishTcs.IsValueCreated) {
                finishTcs.Value.SetResult();
            }
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
