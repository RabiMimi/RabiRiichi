using System.Collections.Generic;

namespace System.Linq {
    public static class RabiEnumerable {
        static RabiEnumerable() {
            InitSubsetIndices();
        }

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

        #region Subset

        private static List<int[]>[][] subsetIndices;
        private const int MAX_SUBSET_SIZE = 7;

        private static void InitSubsetIndices() {
            subsetIndices = new List<int[]>[MAX_SUBSET_SIZE + 1][];
            for (int i = 0; i <= MAX_SUBSET_SIZE; i++) {
                subsetIndices[i] = new List<int[]>[i + 1];
                for (int j = 0; j <= i; j++) {
                    subsetIndices[i][j] = new List<int[]>();
                }
            }
            List<int> indices = new();
            for (uint i = 0, ed = 1 << MAX_SUBSET_SIZE; i < ed; i++) {
                indices.Clear();
                int maxIndex = -1;
                int bitCount = 0;
                for (int j = 0; j < MAX_SUBSET_SIZE; j++) {
                    if (((i >> j) & 1) != 0) {
                        indices.Add(j);
                        maxIndex = j;
                        bitCount++;
                    }
                }
                for (int j = maxIndex + 1; j <= MAX_SUBSET_SIZE; j++) {
                    subsetIndices[j][bitCount].Add(indices.ToArray());
                }
            }
        }

        private static IEnumerable<IEnumerable<TSource>> SubsetHelper<TSource>(List<TSource> list, int n) {
            if (n > list.Count) {
                yield break;
            }
            foreach (var result in list.Skip(1).Subset(n - 1)) {
                yield return result.Prepend(list[0]);
            }
            foreach (var result in list.Skip(1).Subset(n)) {
                yield return result;
            }
        }

        /// <summary>
        /// 生成所有source的大小为n的子集
        /// </summary>
        public static IEnumerable<IEnumerable<TSource>> Subset<TSource>(this IEnumerable<TSource> source, int n) {
            if (source is not List<TSource> list) {
                list = source.ToList();
            }
            if (n < 0 || n > list.Count) {
                yield break;
            }
            if (list.Count > MAX_SUBSET_SIZE) {
                foreach (var result in SubsetHelper(list, n)) {
                    yield return result;
                }
            } else {
                foreach (var indices in subsetIndices[list.Count][n]) {
                    yield return indices.Select(index => list[index]);
                }
            }
        }
        #endregion
    }
}