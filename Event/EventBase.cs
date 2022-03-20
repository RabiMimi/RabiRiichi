﻿using RabiRiichi.Communication;
using RabiRiichi.Riichi;
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
        public Task WaitForFinish => finishTcs.Task;

        /// <summary> 事件处理过程中可能会用到的额外信息 </summary>
        public readonly Dictionary<string, object> extraData = new();

        /// <summary> 该事件的父事件 </summary>
        public readonly EventBase parent;

        /// <summary> 该事件的子事件 </summary>
        public readonly List<EventBase> children = new();

        public EventBase(EventBase parent) {
            if (parent != null) {
                this.parent = parent;
                this.game = parent.game;
                this.parent.children.Add(this);
            }
            id = game.info.eventId.Next;
        }

        public EventBase(Game game) {
            this.game = game;
        }

        /// <summary> 强制取消该事件及其后继事件 </summary>
        public void Cancel() {
            if (IsFinishedOrCancelled) {
                return;
            }
            phase = EventPriority.Cancelled;
            finishTcs.SetCanceled();
            foreach (var child in children) {
                child.Cancel();
            }
        }

        /// <summary> 结束事件 </summary>
        public void Finish() {
            if (IsFinishedOrCancelled) {
                return;
            }
            phase = EventPriority.Finished;
            finishTcs.SetResult();
        }

        /// <summary> 在事件成功处理后执行回调 </summary>
        public void OnFinish(System.Action callback) {
            WaitForFinish.ContinueWith((_) => callback(), TaskContinuationOptions.NotOnCanceled);
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
