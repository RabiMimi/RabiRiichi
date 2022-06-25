using RabiRiichi.Communication;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Generated.Actions;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RabiRiichi.Actions {
    [RabiMessage]
    public class SinglePlayerInquiry : IRabiPlayerMessage {
        [RabiBroadcast] public readonly List<IPlayerAction> actions = new();
        public int playerId { get; init; }
        /// <summary>
        /// 所有操作的最高优先级
        /// </summary>
        public int maxPriority { get; private set; } = int.MinValue;
        /// <summary>
        /// 若用户已经选择操作，该操作的优先级
        /// </summary>
        public int curPriority { get; private set; } = int.MinValue;

        /// <summary>
        /// 用户做出的回应，默认是0（第一个选项）
        /// </summary>
        public int responseIndex = 0;

        public IPlayerAction Selected => actions[responseIndex];
        public bool hasResponded = false;

        public SinglePlayerInquiry(int playerId) {
            this.playerId = playerId;
            AddAction(new SkipAction(playerId), true);
        }

        public SinglePlayerInquiry DisableSkip() {
            var selected = Selected;
            actions.RemoveAll(action => action is SkipAction);
            responseIndex = selected is SkipAction ? 0 : actions.IndexOf(selected);
            return this;
        }

        public SinglePlayerInquiry AddAction(IPlayerAction action, bool isDefault = false) {
            if (action.priority > maxPriority) {
                maxPriority = action.priority;
            }
            if (playerId != action.playerId) {
                throw new InvalidDataException($"Cannot add action for {action.playerId} to {playerId}");
            }
            actions.Add(action);
            if (isDefault) {
                responseIndex = actions.Count - 1;
                curPriority = action.priority;
            }
            return this;
        }

        public bool OnResponse(int index, string response) {
            if (hasResponded) {
                return false;
            }
            if (index >= 0 && index < actions.Count) {
                var action = actions[index];
                if (!action.OnResponse(response)) {
                    return false;
                }
                responseIndex = index;
                curPriority = action.priority;
            }
            hasResponded = true;
            return true;
        }

        public SinglePlayerInquiryMsg ToProto() {
            var ret = new SinglePlayerInquiryMsg();
            ret.Actions.AddRange(actions.Select(action => ProtoConverters.ToProto(action)));
            return ret;
        }
    }
}