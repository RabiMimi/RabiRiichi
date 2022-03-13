namespace RabiRiichi.Util {
    public struct AutoIncrementInt {
        public int Value { get; private set; }
        public int Next => ++Value;
        public void Reset(int value = 0) => Value = value;
        public AutoIncrementInt(int initialValue = 0) {
            Value = initialValue;
        }
        public static implicit operator int(AutoIncrementInt a) => a.Value;
        public static implicit operator AutoIncrementInt(int a) => new(a);
    }
}