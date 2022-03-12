using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RabiRiichi.Action {
    [RabiMessage]
    public class SinglePlayerInquiry : IWithPlayer {
        [RabiBroadcast] public readonly List<IPlayerAction> actions = new();
        public Player player { get; init; }
        /// <summary>
        /// 所有操作的最高优先级
        /// </summary>
        public int maxPriority { get; private set; } = int.MinValue;
        /// <summary>
        /// 若用户已经选择操作，该操作的优先级
        /// </summary>
        public int curPriority { get; private set; } = int.MinValue;

        public int responseIndex = 0;
        public bool hasResponded = false;

        public SinglePlayerInquiry(Player player) {
            this.player = player;
        }

        public SinglePlayerInquiry AddAction(IPlayerAction action, bool isDefault = false) {
            if (action.priority > maxPriority) {
                maxPriority = action.priority;
            }
            if (!player.SamePlayer(action.player)) {
                throw new InvalidDataException($"Cannot add action for {action.player.id} to {player.id}");
            }
            actions.Add(action);
            if (isDefault) {
                responseIndex = actions.Count - 1;
                curPriority = action.priority;
            }
            return this;
        }

        public bool OnResponse(int index, string response) {
            if (hasResponded || index < 0 || index >= actions.Count) {
                return false;
            }
            var action = actions[index];
            if (!action.OnResponse(response)) {
                return false;
            }
            responseIndex = index;
            hasResponded = true;
            curPriority = action.priority;
            return true;
        }

        public Task Trigger() => actions[responseIndex].Trigger();
    }
}