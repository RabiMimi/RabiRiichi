using RabiRiichi.Core;
using RabiRiichi.Patterns;


namespace RabiRiichi.Actions.Resolver {
    public class ChanAnKanResolver : ChanKanResolver {
        private readonly Base13_1 base13_1;
        public ChanAnKanResolver(PatternResolver patternResolver, Base13_1 base13_1) : base(patternResolver) {
            this.base13_1 = base13_1;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            // 暗杠，判定国士
            if (!base13_1.Resolve(player.hand, incoming, out _)) {
                return false;
            }
            return base.ResolveAction(player, incoming, output);
        }
    }
}