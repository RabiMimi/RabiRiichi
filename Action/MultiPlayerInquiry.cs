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

    public class MultiPlayerInquiry {
        public readonly int id;
        public readonly List<SinglePlayerInquiry> playerInquiries = new();
        public readonly TaskCompletionSource taskCompletionSource = new();
        public readonly List<IPlayerAction> responses = new();
        /// <summary> 当前已回应的用户的最高优先级 </summary>
        private int curMaxPriority = int.MinValue;
        public AtomicBool hasExecuted { get; private set; } = new();
        public Task WaitForResponse => IsEmpty ? Task.CompletedTask : taskCompletionSource.Task;
        public bool IsEmpty => playerInquiries.Count == 0;

        public MultiPlayerInquiry(GameInfo info) {
            id = info.eventId.Next;
        }

        /// <summary> 添加一个PlayerAction，仅在构建inquiry被调用，非线程安全 </summary>
        internal MultiPlayerInquiry Add(IPlayerAction action, bool isDefault = false) {
            var list = playerInquiries.Find(x => x.playerId == action.playerId);
            if (list == null) {
                list = new SinglePlayerInquiry(action.playerId, id);
                playerInquiries.Add(list);
            }
            list.AddAction(action, isDefault);
            return this;
        }

        /// <summary> 处理用户的回应，但是不会触发操作 </summary>
        /// <returns> 是否可以终止等待（已无用户可以做出优先级更高或相同的回应） </returns>
        private bool OnResponseWithoutTrigger(InquiryResponse resp) {
            lock (playerInquiries) {
                var list = playerInquiries.Find(x => x.playerId == resp.playerId);
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
        /// 用户的选择将被放入<see cref="responses"/>，然后完成<see cref="WaitForResponse"/>。
        /// 若询问已被终止过，该方法不会产生任何效果
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Finish() {
            if (hasExecuted.Exchange(true)) {
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
            taskCompletionSource.SetResult();
        }
    }
}