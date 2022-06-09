using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
    public class UserList : IEnumerable<User> {
        public readonly ConcurrentDictionary<long, User> users = new();
        private readonly Random rand;

        public UserList(Random rand) {
            this.rand = rand;
        }

        public bool Add(User user) {
            for (int i = 0; i < 10; i++) {
                long sessionCode = rand.NextInt64();
                if (users.TryAdd(sessionCode, user)) {
                    user.sessionCode = sessionCode;
                    return true;
                }
            }
            return false;
        }

        public User Get(long id) {
            return TryGet(id, out var user) ? user : null;
        }

        public bool TryGet(long sessionCode, out User user) {
            return users.TryGetValue(sessionCode, out user);
        }

        public bool TryRemove(long sessionCode, out User user) {
            return users.TryRemove(sessionCode, out user);
        }

        public IEnumerator<User> GetEnumerator() {
            return users.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}