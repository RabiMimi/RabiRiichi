using System;
using System.Threading;

namespace RabiRiichi.Utils {
    internal class MutexHolder : IDisposable {
        private readonly Mutex mutex;

        public MutexHolder(Mutex mutex, int timeoutMs = -1) {
            this.mutex = mutex;
            if (timeoutMs < 0) {
                mutex.WaitOne();
            } else {
                if (!mutex.WaitOne(timeoutMs)) {
                    throw new TimeoutException($"Can't keep up! Is the server overloaded? Running {timeoutMs}ms behind.");
                }
            }
        }

        public void Dispose() {
            mutex.ReleaseMutex();
        }
    }
}