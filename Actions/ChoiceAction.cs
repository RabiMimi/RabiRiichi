using RabiRiichi.Communication;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
    [RabiMessage]
    public abstract class ActionOption { }

    public abstract class ChoiceAction<T> : PlayerAction<T> {
        [RabiBroadcast] public List<ActionOption> options = new();

        public ChoiceAction(int playerId) : base(playerId) { }

        public void AddOption(ActionOption option) {
            options.Add(option);
        }
    }

    public abstract class MultiChoiceAction : ChoiceAction<List<int>> {
        public IEnumerable<ActionOption> chosen => response.Select(r => options[r]);
        public MultiChoiceAction(int playerId) : base(playerId) {
            response = new List<int>();
        }

        public override bool ValidateResponse(List<int> resp) {
            if (resp == null) {
                return false;
            }
            response = resp
                .Where(i => i >= 0 && i < options.Count)
                .OrderBy(i => i)
                .Distinct()
                .ToList();
            return true;
        }
    }

    public abstract class SingleChoiceAction : ChoiceAction<int> {
        public ActionOption chosen => options[response];
        public SingleChoiceAction(int playerId) : base(playerId) {
            response = 0;
        }

        public override bool ValidateResponse(int response)
            => response >= 0 && response < options.Count;
    }
}