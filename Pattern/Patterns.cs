namespace RabiRiichi.Pattern {
    public static class Patterns {
        // TODO: 注入Pattern管理
        public static BasePattern[] BasePatterns = new BasePattern[] {
            new Base33332(),
            new Base72(),
            new Base13_1()
        };
        public static StdPattern[] StdPatterns = new StdPattern[] {
            new Pinfu(),
        };
        public static StdPattern[] BonusPatterns = new StdPattern[] {
            new Akadora(),
        };
    }
}