using RabiRiichi.Util;
using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
    public class UserList : IEnumerable<User> {
        public readonly ConcurrentDictionary<int, User> users = new();
        private readonly AutoIncrementInt idGenerator;

        public UserList() { }

        public bool Add(User user) {
            user.id = idGenerator.Next;
            return users.TryAdd(user.id, user);
        }

        public User Get(int id) {
            return TryGet(id, out var user) ? user : null;
        }

        public bool TryGet(int id, out User user) {
            return users.TryGetValue(id, out user);
        }

        public bool TryRemove(int id, out User user) {
            return users.TryRemove(id, out user);
        }

        public IEnumerator<User> GetEnumerator() {
            return users.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}