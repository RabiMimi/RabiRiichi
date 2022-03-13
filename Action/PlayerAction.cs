using RabiRiichi.Interact;
using RabiRiichi.Util;
using System;
using System.Text.Json;
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

    public interface IPlayerAction : IRabiPlayerMessage {
        int priority { get; }
        bool OnResponse(string response);
    }

    /// <summary> 等待玩家做出选择 </summary>
    [RabiPrivate]
    public abstract class PlayerAction<T> : IPlayerAction {
        [RabiPrivate] public abstract string name { get; }
        [RabiPrivate] public RabiMessageType msgType => RabiMessageType.Action;

        [RabiPrivate] public int playerId { get; init; }

        public int priority { get; protected set; }


        /// <summary>
        /// 初始值必须是一个有效的回应，用于用户超时跳过的情况
        /// </summary>
        protected T response;

        public PlayerAction(int playerId) {
            this.playerId = playerId;
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

        public virtual bool ValidateResponse(T response) {
            return true;
        }
    }
}