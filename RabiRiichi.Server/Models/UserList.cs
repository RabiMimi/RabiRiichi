using System.Collections;
using System.Collections.Concurrent;

namespace RabiRiichi.Server.Models {
    public class UserList : IEnumerable<User> {
        public readonly ConcurrentDictionary<int, User> users = new();
        private readonly Random rand = new((int)
            (DateTimeOffset.Now.ToUnixTimeMilliseconds() & 0xffffffff));

        public bool Add(User user) {
            for (int i = 0; i < 10; i++) {
                int sessionCode = rand.Next();
                if (users.TryAdd(sessionCode, user)) {
                    user.sessionCode = sessionCode;
                    return true;
                }
            }
            return false;
        }

        public User Get(int id) {
            return users.TryGetValue(id, out var user) ? user : null;
        }

        public bool Remove(int id, out User user) {
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