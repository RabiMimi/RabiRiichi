using RabiRiichi.Riichi;

namespace RabiRiichi.Event {
    public static class Priority {
        public const uint Never = 0;
        public const uint Last = 1;
        public const uint MessageSender = (uint)1e3;
        public const uint Low = (uint)1e6;
        public const uint Default = (uint)2e6;
        public const uint High = (uint)3e6;
    }

    public enum Phase {
        /// <summary>
        /// The event handling has not started.
        /// This phase never occurs during event handling.
        /// </summary>
        Inactive,
        /// <summary> Before the event happens </summary>
        Pre,
        /// <summary>
        /// Handles the event, and sets corresponding data.
        /// A special phase because only the top priority listener will be activated.
        /// </summary>
        On,
        /// <summary>
        /// Actually apply the event to the game instance.
        /// </summary>
        Post,
        /// <summary>
        /// After the event is handled.
        /// Should never change event data.
        /// </summary>
        Finalize,
        /// <summary>
        /// The event is already handled.
        /// This phase never occurs during event handling.
        /// </summary>
        Finished
    }

    public abstract class EventBase {
        public Game game;
        public Phase phase = Phase.Inactive;

        public bool IsActive => phase != Phase.Inactive && phase != Phase.Finished;
        public bool IsFinished => phase == Phase.Finished;

        /// <summary> 切换到下一个 phase </summary>
        public bool NextPhase() {
            if (phase == Phase.Finished)
                return false;
            phase++;
            return true;
        }

        /// <summary> 强制结束该事件 </summary>
        public void Finish() {
            phase = Phase.Finished;
        }

        public override string ToString() {
            return $"{GetType().Name}:{phase}";
        }
    }
}
