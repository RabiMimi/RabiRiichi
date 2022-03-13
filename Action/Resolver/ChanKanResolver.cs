using RabiRiichi.Pattern;
using RabiRiichi.Riichi;


namespace RabiRiichi.Action.Resolver {
    public class ChanKanResolver : RonResolver {
        private readonly Base13_1 base13_1;
        public ChanKanResolver(PatternResolver patternResolver, Base13_1 base13_1) : base(patternResolver) {
            this.base13_1 = base13_1;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            if (player.hand.IsFuriten) {
                return false;
            }
            if (incoming.IsTsumo) {
                // 暗杠，判定国士
                if (base13_1.Resolve(player.hand, incoming, out _)) {
                    output.Add(new RonAction(player.id));
                    return true;
                }
                return false;
            }
            return base.ResolveAction(player, incoming, output);
        }
    }
}