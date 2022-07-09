using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Utils {
    public class RabiRand {
        public ulong seed;
        private const ulong MULTIPLIER = 6364136223846793005;
        private const ulong INCREMENT = 1442695040888963407;

        public RabiRand(ulong seed) {
            this.seed = seed;
        }

        public ulong Next() {
            return seed = seed * MULTIPLIER + INCREMENT;
        }

        public int Next(int minValue, int maxValue)
            => minValue + Next(maxValue - minValue);

        public int Next(int maxValue)
            => (int)(Next() % (ulong)maxValue);

        public void Shuffle<T>(IList<T> list) {
            for (int i = 1; i < list.Count; i++) {
                int index = Next(i + 1);
                (list[index], list[i]) = (list[i], list[index]);
            }
        }

        public T Choice<T>(IList<T> list) {
            return list[Next(list.Count)];
        }

        public IEnumerable<T> Choice<T>(IList<T> list, int count) {
            count = Math.Min(count, list.Count);
            List<int> helper = Enumerable.Range(0, list.Count).ToList();
            Shuffle(helper);
            helper.RemoveRange(count, helper.Count - count);
            return helper.Select(index => list[index]);
        }
    }
}
