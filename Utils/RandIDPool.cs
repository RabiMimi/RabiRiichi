using System.Collections.Generic;

namespace RabiRiichi.Utils {
    public class RandIDPool {
        private readonly RabiRand rand;
        private readonly int maxId;
        private readonly HashSet<int> usedIds = new();

        public RandIDPool(RabiRand rand, int maxId = int.MaxValue) {
            this.rand = rand;
            this.maxId = maxId;
        }

        public void Reset() {
            usedIds.Clear();
        }

        public int GetID() {
            int id = rand.Next(maxId);
            while (usedIds.Contains(id)) {
                id = rand.Next(maxId);
            }
            usedIds.Add(id);
            return id;
        }
    }
}
