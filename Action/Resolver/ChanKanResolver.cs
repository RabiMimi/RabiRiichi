using RabiRiichi.Core;
using RabiRiichi.Pattern;


namespace RabiRiichi.Action.Resolver {
    public class ChanKanResolver : RonResolver {
        public ChanKanResolver(PatternResolver patternResolver) : base(patternResolver) { }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            return base.ResolveAction(player, incoming, output);
        }
    }
}