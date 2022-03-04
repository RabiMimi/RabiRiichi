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
        public readonly List<SinglePlayerInquiry> playerInquiries = new();
        public readonly TaskCompletionSource taskCompletionSource = new();
        /// <summary> 当前已回应的用户的最高优先级 </summary>
        private int curMaxPriority = int.MinValue;
        public bool hasExecuted { get; private set; } = false;

        public Task WaitTillFinalized => taskCompletionSource.Task;

        public MultiPlayerInquiry Add(IPlayerAction action) {
            var list = playerInquiries.Find(x => x.player.SamePlayer(action.player));
            if (list == null) {
                list = new SinglePlayerInquiry(action.player);
                playerInquiries.Add(list);
            }
            list.AddAction(action);
            return this;
        }

        /// <summary>
        /// 处理用户的回应，但是不会触发操作
        /// </summary>
        /// <returns>是否可以终止等待（已无用户可以做出优先级更高或相同的回应）</returns>
        public bool OnResponseWithoutTrigger(InquiryResponse resp) {
            var list = playerInquiries.Find(x => x.player.id == resp.playerId);
            if (list == null || !list.OnResponse(resp.index, resp.response)) {
                return false;
            }
            curMaxPriority = Math.Max(curMaxPriority, list.curPriority);
            return playerInquiries.All(x => x.hasResponded || x.maxPriority < curMaxPriority);
        }

        /// <summary>
        /// 处理用户的回应
        /// </summary>
        /// <returns>是否成功处理本次询问。若返回true，应该终止等待，并无视关于此inquiry的后续回应</returns>
        public Task<bool> OnResponse(InquiryResponse resp) {
            if (hasExecuted) {
                return Task.FromResult(false);
            }
            if (OnResponseWithoutTrigger(resp)) {
                return Finalize();
            }
            return Task.FromResult(false);
        }

        public async Task<bool> Finalize() {
            if (hasExecuted) {
                return false;
            }
            hasExecuted = true;
            if (curMaxPriority == int.MinValue) {
                // 没有用户做出有效回应
                curMaxPriority = playerInquiries.Max(x => x.curPriority);
            }
            // 仅触发优先级最高的操作（可能有多个用户）
            foreach (var action in playerInquiries.Where(x => x.curPriority == curMaxPriority)) {
                await action.Trigger();
            }
            taskCompletionSource.SetResult();
            return true;
        }
    }
}