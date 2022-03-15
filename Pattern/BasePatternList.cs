using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class BasePatternList : List<BasePattern> {
        public BasePatternList(params BasePattern[] basePatterns) {
            AddRange(basePatterns);
        }
    }

    public class NoPattern : BasePatternList {
        public NoPattern() : base() { }
    }

    public class AllBasePatterns : BasePatternList {
        public AllBasePatterns(Base33332 base33332, Base13_1 base13_1, Base72 base72)
            : base(base33332, base13_1, base72) { }
    }

    public class AllExcept13_1 : BasePatternList {
        public AllExcept13_1(Base33332 base33332, Base72 base72)
            : base(base33332, base72) { }
    }
}