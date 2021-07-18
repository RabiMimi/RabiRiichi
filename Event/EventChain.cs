using System.Collections.Generic;

namespace RabiRiichi.Event {
    class EventChain: EventBase {
        #region Request
        /// <summary>
        /// 来源事件
        /// </summary>
        public EventBase source = null;
        #endregion

        #region Response
        /// <summary>
        /// 触发事件
        /// </summary>
        public List<EventBase> toTrigger = new List<EventBase>();

        /// <summary>
        /// 是否取消来源事件
        /// </summary>
        public bool cancelSource = false;
        #endregion
    }
}
