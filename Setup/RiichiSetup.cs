using RabiRiichi.Pattern;

namespace RabiRiichi.Setup {
    public class RiichiSetup : BaseSetup {
        protected override void RegisterBasePatterns(PatternResolver resolver) {
            resolver.RegisterBasePattern(new Base33332());
            resolver.RegisterBasePattern(new Base72());
            resolver.RegisterBasePattern(new Base13_1());
        }

        protected override void RegisterStdPatterns(PatternResolver resolver) {
            resolver.RegisterStdPattern(new 一杯口());
            resolver.RegisterStdPattern(new 平和());
            resolver.RegisterStdPattern(new 役牌中());
            resolver.RegisterStdPattern(new 役牌发());
            resolver.RegisterStdPattern(new 役牌白());
            resolver.RegisterStdPattern(new 役牌自风());
            resolver.RegisterStdPattern(new 役牌场风());
            resolver.RegisterStdPattern(new 断幺九());
            resolver.RegisterStdPattern(new 门清自摸和());
        }

        protected override void RegisterBonusPatterns(PatternResolver resolver) {
            resolver.RegisterBonusPattern(new 赤宝牌());
        }
    }
}