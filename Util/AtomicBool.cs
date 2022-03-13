using System.Threading;

namespace RabiRiichi.Util {
    public struct AtomicBool {
        private const int FALSE = 0;
        private const int TRUE = 1;
        private int value;
        public AtomicBool(bool value = false) {
            this.value = value ? TRUE : FALSE;
        }
        public bool Exchange(bool newValue) {
            return Interlocked.Exchange(ref value, newValue ? TRUE : FALSE) == TRUE;
        }
        public void Set(bool newValue) {
            value = newValue ? TRUE : FALSE;
        }
        public static implicit operator AtomicBool(bool value) => new(value);
        public static implicit operator bool(AtomicBool b) => b.value == TRUE;
    }
}