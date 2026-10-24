using System;

namespace RabiRiichi.Utils {
  public static class Extensions {
    public static int ToInt(this bool val) {
      return val ? 1 : 0;
    }

    public static int CeilTo100(this int val) {
      return (val + 99) / 100 * 100;
    }

    public static long CeilTo100(this long val) {
      long remainder = val % 100;
      if (remainder == 0) return val;
      long increment = remainder > 0 ? 100 - remainder : -remainder;
      return val > long.MaxValue - increment ? long.MaxValue : val + increment;
    }

    public static bool HasAnyFlag<T>(this T val, T flags) where T : struct, Enum {
      return ((int)(object)val & (int)(object)flags) != 0;
    }

    public static bool HasAllFlags<T>(this T val, T flags) where T : struct, Enum {
      return ((int)(object)val & (int)(object)flags) == (int)(object)flags;
    }
  }
}
