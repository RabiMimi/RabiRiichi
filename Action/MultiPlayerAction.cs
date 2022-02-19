using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public class SinglePlayerAction : List<PlayerAction> {
        public readonly Player player;
        public int maxPriority { get; private set; }

        public int responseIndex = int.MinValue;
        public int response = int.MinValue;
        public bool HasResponse => responseIndex != int.MinValue;
        public bool HasSkipped => responseIndex == -1;

        public SinglePlayerAction(Player player) {
            this.player = player;
        }

        public void AddAction(PlayerAction action) {
            if (action.Priority > maxPriority) {
                maxPriority = action.Priority;
            }
            if (!player.SamePlayer(action.player)) {
                throw new InvalidDataException($"Cannot add action for {action.player.id} to {player.id}");
            }
            Add(action);
        }

        public Task<bool> Trigger() {
            if (!HasResponse || HasSkipped) {
                return Task.FromResult(false);
            }
            var action = this[responseIndex];
            return action.onResponse(response);
        }
    }

    public class MultiPlayerAction {
        public List<SinglePlayerAction> actions = new List<SinglePlayerAction>();

        public MultiPlayerAction Add(PlayerAction action) {
            var list = actions.Find(x => x.player.SamePlayer(action.player));
            if (list == null) {
                list = new SinglePlayerAction(action.player);
                actions.Add(list);
            }
            list.AddAction(action);
            return this;
        }

        public bool OnResponse(Player player, int index, int response) {
            var list = actions.Find(x => x.player.SamePlayer(player));
            if (list == null || list.HasResponse) {
                return false;
            }
            if (index < -1 || index >= list.Count) {
                return false;
            }
            list.responseIndex = index;
            list.response = response;
            return actions.All(x => x.HasResponse || x.maxPriority <= list.maxPriority);
        }

        public async Task<bool> Finalize() {
            actions.Sort((x, y) => y.maxPriority.CompareTo(x.maxPriority));
            foreach (var action in actions) {
                if (await action.Trigger()) {
                    return true;
                }
            }
            return false;
        }
    }
}