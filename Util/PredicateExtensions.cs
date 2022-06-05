using RabiRiichi.Pattern;
using System;
using PatternPredicate = System.Predicate<System.Collections.Generic.ICollection<RabiRiichi.Pattern.StdPattern>>;

namespace RabiRiichi.Util {
    public static class PredicateExtensions {
        public static PatternPredicate GetPredicate(this StdPattern pattern) {
            return patterns => patterns.Contains(pattern);
        }

        public static Predicate<T> Or<T>(this Predicate<T> predicate, Predicate<T> other) {
            return patterns => predicate(patterns) || other(patterns);
        }

        public static PatternPredicate Or(this StdPattern pattern, PatternPredicate other) {
            return GetPredicate(pattern).Or(other);
        }

        public static PatternPredicate Or(this PatternPredicate predicate, StdPattern other) {
            return predicate.Or(GetPredicate(other));
        }

        public static PatternPredicate Or(this StdPattern pattern, StdPattern other) {
            return GetPredicate(pattern).Or(GetPredicate(other));
        }

        public static Predicate<T> And<T>(this Predicate<T> predicate, Predicate<T> other) {
            return patterns => predicate(patterns) && other(patterns);
        }

        public static PatternPredicate And(this StdPattern pattern, PatternPredicate other) {
            return GetPredicate(pattern).And(other);
        }

        public static PatternPredicate And(this PatternPredicate predicate, StdPattern other) {
            return predicate.And(GetPredicate(other));
        }

        public static PatternPredicate And(this StdPattern pattern, StdPattern other) {
            return GetPredicate(pattern).And(GetPredicate(other));
        }

        public static Predicate<T> Not<T>(this Predicate<T> predicate) {
            return patterns => !predicate(patterns);
        }

        public static PatternPredicate Not(this StdPattern pattern) {
            return GetPredicate(pattern).Not();
        }
    }
}