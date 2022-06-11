namespace System.Threading {
    public static class RabiInterlocked {
        public static void ExchangeMax(ref int location, int value) {
            int initialValue, result;
            do {
                initialValue = location;
                result = Math.Max(initialValue, value);
            } while (Interlocked.CompareExchange(ref location, result, initialValue) != initialValue);
        }
    }
}