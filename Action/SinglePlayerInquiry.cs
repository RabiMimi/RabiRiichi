using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RabiRiichi.Action {
    public class SinglePlayerInquiry {
        public readonly List<IPlayerAction> actions = new();
        public readonly Player player;
        public int maxPriority { get; private set; }

        public int responseIndex = int.MinValue;
        public string response;
        public bool hasResponded = false;

        public SinglePlayerInquiry(Player player) {
            this.player = player;
        }

        public void AddAction(IPlayerAction action, bool isDefault = false) {
            if (action.priority > maxPriority) {
                maxPriority = action.priority;
            }
            if (!player.SamePlayer(action.player)) {
                throw new InvalidDataException($"Cannot add action for {action.player.id} to {player.id}");
            }
            actions.Add(action);
            if (isDefault) {
                responseIndex = actions.Count - 1;
            }
        }

        public Task<bool> Trigger() {
            if (responseIndex < 0 || responseIndex >= actions.Count) {
                return Task.FromResult(false);
            }
            var action = actions[responseIndex];
            return action.OnResponse(response);
        }
    }
}