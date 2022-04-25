namespace RabiRiichi.Util {
    public static class Extensions {
        public static int ToInt(this bool val) {
            return val ? 1 : 0;
        }

        public static int CeilTo100(this int val) {
            return (val + 99) / 100 * 100;
        }
    }
}
