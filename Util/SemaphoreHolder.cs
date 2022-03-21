using System;
using System.Threading;

namespace RabiRiichi.Util {
    internal class SemaphoreHolder : IDisposable {
        private readonly SemaphoreSlim semaphore;

        public SemaphoreHolder(SemaphoreSlim semaphore, int timeoutMs = -1) {
            this.semaphore = semaphore;
            if (timeoutMs < 0) {
                semaphore.Wait();
            } else {
                if (!semaphore.Wait(timeoutMs)) {
                    throw new TimeoutException($"Can't keep up! Is the server overloaded? Running {timeoutMs}ms behind.");
                }
            }
        }

        public void Dispose() {
            semaphore.Release();
        }
    }
}