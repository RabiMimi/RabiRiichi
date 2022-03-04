using RabiRiichi.Riichi;
using RabiRiichi.Util;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RabiRiichi.Action {
    /// <summary> 优先级较高的Action触发后，停止等待其他Action的触发 </summary>
    public static class ActionPriority {
        public const int ChooseTile = 10000;
        public const int Skip = 1000;
        public const int Chi = 2000;
        public const int Pon = 3000;
        public const int Kan = 4000;

        public const int Ron = 5000;
        public const int Riichi = 10000;
        public const int Discard = 10000;
    }

    public interface IPlayerAction {
        Player player { get; }
        int playerId { get; }
        int priority { get; }
        string id { get; }
        bool OnResponse(string response);
        Task Trigger();
    }

    /// <summary> 等待玩家做出选择 </summary>
    public abstract class PlayerAction<T> : IPlayerAction {
        [JsonIgnore]
        public Player player { get; }

        [JsonInclude]
        public int playerId { get; }

        [JsonIgnore]
        public int priority { get; protected set; }

        [JsonInclude]
        public abstract string id { get; }

        /// <summary>
        /// 初始值必须是一个有效的回应，用于用户超时跳过的情况
        /// </summary>
        [JsonIgnore]
        protected T response = default;

        [JsonIgnore]
        public Func<T, Task> onResponse { get; set; }

        public PlayerAction(Player player) {
            this.player = player;
            playerId = player.id;
        }

        public bool OnResponse(string response) {
            T resp;
            try {
                resp = JsonSerializer.Deserialize<T>(response);
                if (ValidateResponse(resp)) {
                    this.response = resp;
                    return true;
                }
            } catch (Exception e) {
                Logger.Warn(e);
            }
            return false;
        }

        public virtual Task Trigger() => onResponse == null ? Task.CompletedTask : onResponse(response);

        public virtual bool ValidateResponse(T response) => true;
    }
}