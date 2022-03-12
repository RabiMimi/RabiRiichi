using RabiRiichi.Interact;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action {
    public abstract class ActionOption : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
    }

    public abstract class ChoiceAction<T> : PlayerAction<T> {
        [RabiBroadcast] public List<ActionOption> choices = new();

        public ChoiceAction(Player player) : base(player) { }

        public void AddOption(ActionOption option) {
            choices.Add(option);
        }
    }

    public abstract class MultiChoiceAction : ChoiceAction<List<int>> {
        public MultiChoiceAction(Player player) : base(player) {
            response = new List<int>();
        }

        public override bool ValidateResponse(List<int> resp) {
            if (resp == null) {
                return false;
            }
            response = response
                .Where(i => i >= 0 && i < choices.Count)
                .OrderBy(i => i)
                .Distinct()
                .ToList();
            return true;
        }
    }

    public abstract class SingleChoiceAction : ChoiceAction<int> {
        public SingleChoiceAction(Player player) : base(player) {
            response = 0;
        }

        public override bool ValidateResponse(int response)
            => response >= 0 && response < choices.Count;
    }
}