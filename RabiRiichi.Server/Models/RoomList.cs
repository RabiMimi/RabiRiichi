using RabiRiichi.Server.Utils;
using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
  public class RoomTaskQueue(RoomList roomList, UserList userList) : TaskQueue(1024) {
    public readonly RoomList roomList = roomList;
    public readonly UserList userList = userList;

    public Task<T> ExecuteAsync<T>(Func<RoomTaskQueue, Task<T>> worker) {
      return ExecuteAsync(() => worker(this));
    }

    public Task ExecuteAsync(Func<RoomTaskQueue, Task> worker) {
      return ExecuteAsync(() => worker(this));
    }

    public Task<T> Execute<T>(Func<RoomTaskQueue, T> worker) {
      return Execute(() => worker(this));
    }

    public Task Execute(Action<RoomTaskQueue> worker) {
      return Execute(() => worker(this));
    }
  }

  public class RoomList(Random rand) : IEnumerable<Room> {
    public readonly ConcurrentDictionary<int, Room> rooms = new();
    private readonly Random rand = rand;

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