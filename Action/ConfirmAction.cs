using System.Threading.Tasks;
using System;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    public abstract class ConfirmAction : PlayerAction {
        public Func<Task<bool>> onConfirm;
        public Func<Task<bool>> onReject;
        public ConfirmAction(Player player) : base(player) {
            this.onResponse = (int index) => {
                if (index == 1) {
                    return onConfirm();
                } else {
                    return onReject();
                }
            };
        }
    }
}