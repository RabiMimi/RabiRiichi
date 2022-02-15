namespace RabiRiichi.Pattern {
    public static class Patterns {
        // TODO: 注入Pattern管理
        public static BasePattern[] BasePatterns = new BasePattern[] {
            new Base33332(),
            new Base72(),
            new Base13_1()
        };
        public static StdPattern[] StdPatterns = new StdPattern[] {
            // 一番
            new Pinfu(),
            new 断幺九(),
            new 役牌场风(),
            new 役牌自风(),
            new 役牌白(),
            new 役牌发(),
            new 役牌中(),
        };
        public static StdPattern[] BonusPatterns = new StdPattern[] {
            new Akadora(),
        };
    }
}