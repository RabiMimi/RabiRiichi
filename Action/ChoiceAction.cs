using System.Collections.Generic;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public abstract class ChoiceAction<T> : PlayerAction {
        public List<T> choices;
        public ChoiceAction(Player player, List<T> choices) : base(player) {
            this.choices = choices;
        }
    }
}