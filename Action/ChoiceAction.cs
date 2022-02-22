using System.Collections.Generic;
using System.Text.Json;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public abstract class ActionOption {
        public virtual string id { get; }
    }

    public abstract class ChoiceAction : PlayerAction {
        public List<ActionOption> choices = new();
        public ChoiceAction(Player player) : base(player) { }

        public void AddOption(ActionOption option) {
            choices.Add(option);
        }
    }
}