using System.Collections.Generic;

namespace System.Linq {
    public static class KiraEnumerable {
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
    }
}