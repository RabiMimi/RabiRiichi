using System.Threading;

namespace RabiRiichi.Utils {
    public struct AutoIncrementInt {
        private int value;
        public int Value => value;
        public int Next => Interlocked.Increment(ref value);
        public void Increase() => _ = Next;
        public void Reset(int value = 0) => this.value = value;
        public AutoIncrementInt(int initialValue = 0) {
            value = initialValue;
        }
        public static implicit operator int(AutoIncrementInt a) => a.Value;
        public static implicit operator AutoIncrementInt(int a) => new(a);
    }
}