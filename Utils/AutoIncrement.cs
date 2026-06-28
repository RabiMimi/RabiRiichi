using System.Threading;

namespace RabiRiichi.Utils {
  public struct AutoIncrementInt(int initialValue = 0) {
    private int value = initialValue;
    public readonly int Value => value;
    public int Next => Interlocked.Increment(ref value);
    public void Increase() {
      _ = Next;
    }

    public void Reset(int value = 0) {
      this.value = value;
    }

    public static implicit operator int(AutoIncrementInt a) {
      return a.Value;
    }

    public static implicit operator AutoIncrementInt(int a) {
      return new(a);
    }
  }
}