using RabiRiichi.Server.Utils;
using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
    public class RoomTaskQueue : TaskQueue {
        public readonly RoomList roomList;
        public readonly UserList userList;

        public RoomTaskQueue(RoomList roomList, UserList userList) : base(1024) {
            this.roomList = roomList;
            this.userList = userList;
        }

        public Task<T> ExecuteAsync<T>(Func<RoomTaskQueue, Task<T>> worker)
            => ExecuteAsync(() => worker(this));

        public Task ExecuteAsync(Func<RoomTaskQueue, Task> worker)
            => ExecuteAsync(() => worker(this));

        public Task<T> Execute<T>(Func<RoomTaskQueue, T> worker)
            => Execute(() => worker(this));

        public Task Execute(Action<RoomTaskQueue> worker)
            => Execute(() => worker(this));
    }

    public class RoomList : IEnumerable<Room> {
        public readonly ConcurrentDictionary<int, Room> rooms = new();
        private readonly Random rand;

        public RoomList(Random rand) {
            this.rand = rand;
        }

        public bool Add(Room room) {
            room.roomList = this;
            for (int i = 0; i < 10; i++) {
                int id = rand.Next(1000, 10000);
                if (rooms.TryAdd(id, room)) {
                    room.id = id;
                    return true;
                }
            }
            return false;
        }

        public Room Get(int id) {
            return TryGet(id, out var room) ? room : null;
        }

        public bool TryGet(int id, out Room room) {
            return rooms.TryGetValue(id, out room);
        }

        public bool Remove(int id) {
            return rooms.TryRemove(id, out _);
        }

        public bool TryRemove(int id, out Room room) {
            return rooms.TryRemove(id, out room);
        }

        public IEnumerator<Room> GetEnumerator() {
            return rooms.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}