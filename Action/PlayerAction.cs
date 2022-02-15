using System;
using System.Threading.Tasks;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    /// <summary> 等待玩家做出选择 </summary>
    public abstract class PlayerAction {
        public Player player;
        public abstract int Priority { get; }

        public PlayerAction(Player player) {
            this.player = player;
        }

        public Func<int, Task<bool>> onResponse;
    }
}