using System;
using System.Threading.Tasks;
using RabiRiichi.Riichi;

namespace RabiRiichi.Action {
    /// <summary> 等待玩家做出选择 </summary>
    public class UserAction<T> {
        public Player player;

        public UserAction(Player player) {
            this.player = player;
        }

        public Func<Task, T> onResponse;
    }
}