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
        public List<SinglePlayerInquiry> playerInquiries = new();
        /// <summary> 当前已回应的用户的最高优先级 </summary>
        private int maxPriority = int.MinValue;
        private bool hasExecuted = false;

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
        /// 处理用户的回应
        /// </summary>
        /// <returns>是否可以终止等待（已无用户可以做出优先级更高或相同的回应）</returns>
        public bool OnResponse(InquiryResponse resp) {
            var list = playerInquiries.Find(x => x.player.id == resp.playerId);
            if (list == null || list.hasResponded) {
                return false;
            }
            if (resp.index < -1 || resp.index >= list.actions.Count) {
                return false;
            }
            list.responseIndex = resp.index;
            list.response = resp.response;
            list.hasResponded = true;
            maxPriority = Math.Max(maxPriority, list.actions[list.responseIndex].priority);
            return playerInquiries.All(x => x.hasResponded || x.maxPriority < maxPriority);
        }

        public async Task<bool> Finalize() {
            if (hasExecuted) {
                return false;
            }
            hasExecuted = true;
            playerInquiries.Sort((x, y) => y.maxPriority.CompareTo(x.maxPriority));
            foreach (var action in playerInquiries) {
                if (await action.Trigger()) {
                    return true;
                }
            }
            return false;
        }
    }
}