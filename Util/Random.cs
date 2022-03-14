using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Util {
    public class Rand : Random {
        public Rand(int seed) : base(seed) { }

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
