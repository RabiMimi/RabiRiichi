using System.Collections.Generic;

namespace System.Linq {
    public static class RabiEnumerable {
        static RabiEnumerable() {
            InitSubsetIndices();
        }

        //public static TSource MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        //    var maxSource = source.First();
        //    var maxKey = selector(maxSource);
        //    foreach (var item in source.Skip(1)) {
        //        var key = selector(item);
        //        if (key.CompareTo(maxKey) > 0) {
        //            maxKey = key;
        //            maxSource = item;
        //        }
        //    }
        //    return maxSource;
        //}

        //public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TKey : IComparable<TKey> {
        //    var minSource = source.First();
        //    var minKey = selector(minSource);
        //    foreach (var item in source.Skip(1)) {
        //        var key = selector(item);
        //        if (key.CompareTo(minKey) < 0) {
        //            minKey = key;
        //            minSource = item;
        //        }
        //    }
        //    return minSource;
        //}

        public static bool All<TSource>(this IEnumerable<TSource> source, Func<TSource, int, bool> predicate) {
            bool ret = true;
            var list = source.ToList();
            for (int i = 0; i < list.Count; i++) {
                if (!predicate(list[i], i)) {
                    ret = false;
                    break;
                }
            }
            return ret;
        }

        public static bool SequenceEqualAfterSort<TSource, TKey>(this IEnumerable<TSource> source, IEnumerable<TSource> other, Func<TSource, TKey> orderBy) {
            return source.OrderBy(orderBy).SequenceEqual(other.OrderBy(orderBy));
        }

        public static bool SequenceEqualAfterSort<TSource>(this IEnumerable<TSource> source, IEnumerable<TSource> other) {
            return source.SequenceEqualAfterSort(other, x => x);
        }

        #region Subset

        private static List<int[]>[] subsetIndices;
        private const int MAX_SUBSET_SIZE = 7;

        private static void InitSubsetIndices() {
            subsetIndices = new List<int[]>[MAX_SUBSET_SIZE + 1];
            for (int i = 0; i <= MAX_SUBSET_SIZE; i++) {
                subsetIndices[i] = new List<int[]>();
            }
            List<int> indices = new();
            for (uint i = 0, ed = 1 << MAX_SUBSET_SIZE; i < ed; i++) {
                indices.Clear();
                int bitCount = 0;
                for (int j = 0; j < MAX_SUBSET_SIZE; j++) {
                    if (((i >> j) & 1) != 0) {
                        indices.Add(j);
                        bitCount++;
                    }
                }
                subsetIndices[bitCount].Add(indices.ToArray());
            }
        }

        private static IEnumerable<IEnumerable<TSource>> SubsetHelper<TSource>(List<TSource> list, int l, int n) {
            int remainingCount = list.Count - l;
            if (n < 0 || n > remainingCount) {
                yield break;
            }
            if (n == 0) {
                yield return Enumerable.Empty<TSource>();
                yield break;
            }
            if (remainingCount > MAX_SUBSET_SIZE) {
                foreach (var result in SubsetHelper(list, l + 1, n - 1)) {
                    yield return result.Prepend(list[0]);
                }
                foreach (var result in SubsetHelper(list, l + 1, n)) {
                    yield return result;
                }
            } else {
                foreach (var indices in subsetIndices[n]) {
                    if (indices[n - 1] >= remainingCount) {
                        break;
                    }
                    yield return indices.Select(index => list[index + l]);
                }
            }
        }

        /// <summary>
        /// 生成所有source的大小为size的子集
        /// </summary>
        public static IEnumerable<IEnumerable<TSource>> Subset<TSource>(this IEnumerable<TSource> source, int size) {
            if (source is not List<TSource> list) {
                list = source.ToList();
            }
            return SubsetHelper(list, 0, size);
        }
        #endregion
    }
}