using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RabiRiichi.Action {
    public abstract class ActionOption { }

    public abstract class ChoiceAction<T> : PlayerAction<T> {
        [JsonInclude]
        public List<ActionOption> choices = new();

        public ChoiceAction(Player player) : base(player) { }

        public void AddOption(ActionOption option) {
            choices.Add(option);
        }
    }

    public abstract class MultiChoiceAction : ChoiceAction<List<int>> {
        public MultiChoiceAction(Player player) : base(player) {
            defaultResponse = new List<int>();
        }

        public override Task<List<int>> ValidateResponse(List<int> response) {
            if (response != null) {
                response = response
                    .Where(i => i >= 0 && i < choices.Count)
                    .OrderBy(i => i)
                    .Distinct()
                    .ToList();
            } else {
                response = defaultResponse;
            }
            return Task.FromResult(response);
        }
    }

    public abstract class SingleChoiceAction : ChoiceAction<int> {
        public SingleChoiceAction(Player player) : base(player) {
            defaultResponse = 0;
        }

        public override Task<int> ValidateResponse(int response) {
            if (response < 0 || response >= choices.Count) {
                response = defaultResponse;
            }
            return Task.FromResult(response);
        }
    }
}