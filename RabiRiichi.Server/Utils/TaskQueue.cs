using Grpc.Core;
using System.Threading.Channels;

namespace RabiRiichi.Server.Utils {
    abstract class WorkerItem {
        public abstract Task Run();
    }

    class WorkerItem<T> : WorkerItem {
        public readonly Func<Task<T>> worker;

        public readonly TaskCompletionSource<T> tcs = new();

        public Task<T> WaitTask => tcs.Task;

        public override async Task Run() {
            try {
                tcs.SetResult(await worker());
            } catch (Exception e) {
                tcs.SetException(e);
            }
        }

        public WorkerItem(Func<Task<T>> worker) {
            this.worker = worker;
        }
    }

    public class TaskQueue {
        private readonly Channel<WorkerItem> channel;
        private readonly CancellationTokenSource cts = new();

        public TaskQueue(int capacity) {
            channel = Channel.CreateBounded<WorkerItem>(new BoundedChannelOptions(capacity) {
                SingleReader = true,
            });
            _ = WorkerLoop();
        }

        private async Task WorkerLoop() {
            try {
                while (true) {
                    var item = await channel.Reader.ReadAsync(cts.Token);
                    await item.Run();
                }
            } catch (OperationCanceledException) {
                // Queue is closed.
            }
        }

        public Task<T> ExecuteAsync<T>(Func<Task<T>> worker) {
            var item = new WorkerItem<T>(worker);
            if (!channel.Writer.TryWrite(item)) {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "Server is busy"));
            }
            return item.WaitTask;
        }

        public Task ExecuteAsync(Func<Task> worker) {
            var item = new WorkerItem<bool>(async () => {
                await worker();
                return true;
            });
            if (!channel.Writer.TryWrite(item)) {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "Server is busy"));
            }
            return item.WaitTask;
        }

        public Task<T> Execute<T>(Func<T> worker)
            => ExecuteAsync(() => Task.FromResult(worker()));

        public Task Execute(Action worker)
            => ExecuteAsync(() => {
                worker();
                return Task.FromResult(true);
            });

        public void Close() {
            cts.Cancel();
        }
    }
}