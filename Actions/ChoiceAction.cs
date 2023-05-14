using RabiRiichi.Communication;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions {
    [RabiMessage]
    public abstract class ActionOption { }

    public interface IChoiceAction {
        public int OptionCount { get; }
    }

    public abstract class ChoiceAction<T, U> : PlayerAction<T>, IChoiceAction where U : ActionOption {
        [RabiBroadcast] public List<U> options = new();
        public int OptionCount => options.Count;

        public ChoiceAction(int playerId) : base(playerId) { }

        public void AddOption(U option) {
            options.Add(option);
        }
    }

    public abstract class MultiChoiceAction<T> : ChoiceAction<List<int>, T> where T : ActionOption {
        public IEnumerable<T> chosen => response.Select(r => options[r]);
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

    public abstract class SingleChoiceAction<T> : ChoiceAction<int, T> where T : ActionOption {
        public T chosen => options[response];
        public SingleChoiceAction(int playerId) : base(playerId) {
            response = 0;
        }

        public override bool ValidateResponse(int response)
            => response >= 0 && response < options.Count;
    }
}