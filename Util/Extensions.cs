using System;

namespace RabiRiichi.Util {
    public static class Extensions {
        public static int ToInt(this bool val) {
            return val ? 1 : 0;
        }

        public static int CeilTo100(this int val) {
            return (val + 99) / 100 * 100;
        }

        public static bool HasAnyFlag<T>(this T val, T flags) where T : struct, Enum {
            return ((int)(object)val & (int)(object)flags) != 0;
        }

        public static bool HasAllFlags<T>(this T val, T flags) where T : struct, Enum {
            return ((int)(object)val & (int)(object)flags) == (int)(object)flags;
        }
    }
}
