using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Action {
    public class InquiryResponse {
        public readonly int playerId;
        public readonly int index;
        public readonly string response;
        public InquiryResponse(int playerId, int index, string response) {
            this.playerId = playerId;
            this.index = index;
            this.response = response;
        }
    }

    public interface IResponseHandler {
        bool Handle(IPlayerAction option);
    }

    public class ResponseHandler<T> : IResponseHandler where T : IPlayerAction {
        public readonly Action<T> handler;
        public ResponseHandler(Action<T> handler) {
            this.handler = handler;
        }
        public bool Handle(IPlayerAction action) {
            if (action is T t) {
                handler(t);
                return true;
            }
            return false;
        }
    }

    public class MultiPlayerInquiry {
        public int id { get; private set; }
        public readonly List<SinglePlayerInquiry> playerInquiries = new();
        private readonly TaskCompletionSource finishTcs = new();
        public readonly List<IPlayerAction> responses = new();
        public readonly List<IResponseHandler> responseHandlers = new();
        /// <summary> 当前已回应的用户的最高优先级 </summary>
        private int curMaxPriority = int.MinValue;
        public AtomicBool hasExecuted { get; private set; } = new();
        public Task WaitForFinish => IsEmpty ? Task.CompletedTask : finishTcs.Task;
        public bool IsEmpty => playerInquiries.Count == 0;
        public readonly Game game;

        public MultiPlayerInquiry(Game game) {
            this.game = game;
        }

        public SinglePlayerInquiry GetByPlayerId(int playerId)
            => playerInquiries.Find(inquiry => inquiry.playerId == playerId);

        /// <summary> 添加一个PlayerAction，仅在构建inquiry被调用，非线程安全 </summary>
        public MultiPlayerInquiry Add(IPlayerAction action, bool isDefault = false) {
            var list = GetByPlayerId(action.playerId);
            if (list == null) {
                list = new SinglePlayerInquiry(action.playerId);
                playerInquiries.Add(list);
            }
            list.AddAction(action, isDefault);
            return this;
        }

        /// <summary> 添加一个用户操作的处理函数，仅在构建inquiry被调用，非线程安全 </summary>
        public MultiPlayerInquiry AddHandler<T>(Action<T> handler) where T : IPlayerAction {
            responseHandlers.Add(new ResponseHandler<T>(handler));
            return this;
        }

        /// <summary> 开始处理询问之前，计算ID </summary>
        internal void BeforeProcess() {
            id = game.info.eventId.Next;
            foreach (var inquiry in playerInquiries) {
                inquiry.id = id;
            }
        }

        /// <summary> 处理用户的回应，但是不会触发操作 </summary>
        /// <returns> 是否可以终止等待（已无用户可以做出优先级更高或相同的回应） </returns>
        private bool OnResponseWithoutTrigger(InquiryResponse resp) {
            lock (playerInquiries) {
                var list = GetByPlayerId(resp.playerId);
                if (list == null || !list.OnResponse(resp.index, resp.response)) {
                    return false;
                }
                curMaxPriority = Math.Max(curMaxPriority, list.curPriority);
                return playerInquiries.All(x => x.hasResponded || x.maxPriority < curMaxPriority);
            }
        }

        /// <summary> 处理用户的回应，线程安全 </summary>
        /// <returns>
        /// 是否成功处理本次询问。
        /// 若返回true，应该终止等待，并无视关于此inquiry的后续回应
        /// </returns>
        public bool OnResponse(InquiryResponse resp) {
            if (hasExecuted) {
                return true;
            }
            if (OnResponseWithoutTrigger(resp)) {
                Finish();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 强制终止询问，未作出选择的用户将选择默认选项，线程安全。
        /// 用户的选择将被放入<see cref="responses"/>，然后完成<see cref="WaitForFinish"/>。
        /// 若询问已被终止过，该方法不会产生任何效果
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Finish() {
            if (hasExecuted.Exchange(true)) {
                return;
            }
            if (IsEmpty) {
                return;
            }
            if (curMaxPriority == int.MinValue) {
                // 没有用户做出有效回应
                curMaxPriority = playerInquiries.Max(x => x.curPriority);
            }
            // 仅触发优先级最高的操作（可能有多个用户）
            if (responses.Count > 0) {
                throw new InvalidOperationException("MultiPlayerInquiry.OnResponse: responses already populated (multithread error?)");
            }
            responses.AddRange(playerInquiries
                .Where(x => x.curPriority == curMaxPriority)
                .Select(x => x.Selected));

            // 处理操作
            foreach (var action in responses) {
                foreach (var handler in responseHandlers) {
                    handler.Handle(action);
                }
            }
            finishTcs.SetResult();
        }
    }
}