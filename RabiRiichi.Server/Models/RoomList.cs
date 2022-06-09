using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
    public class RoomList : IEnumerable<Room> {
        private int lastRoomId;
        public int LastRoomId => lastRoomId;
        public readonly ConcurrentDictionary<int, Room> rooms = new();

        public bool Add(Room room) {
            for (int i = 0; i < 10; i++) {
                int id = Interlocked.Increment(ref lastRoomId);
                if (rooms.TryAdd(id, room)) {
                    room.id = id;
                    return true;
                }
            }
            return false;
        }

        public Room Get(int id) {
            return rooms.TryGetValue(id, out var room) ? room : null;
        }

        public bool Remove(int id, out Room room) {
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