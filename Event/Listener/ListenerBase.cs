using System;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public abstract class ListenerBase : IDisposable {
        public bool IsDisposed { get; protected set; } = false;

        /// <summary>
        /// Whether this event handler is able to handle this event.
        /// </summary>
        /// <returns>Priority.</returns>
        public virtual uint CanListen(EventBase ev) => Priority.Never;

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <returns>
        /// True if other event handler in the same phase should be skipped.
        /// Usually return false unless handling Phase.On.
        /// </returns>
        public abstract Task<bool> Handle(EventBase ev);

        public virtual void Dispose() {
            IsDisposed = true;
        }
    }
}
