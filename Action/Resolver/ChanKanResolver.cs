using RabiRiichi.Core;
using RabiRiichi.Pattern;


namespace RabiRiichi.Action.Resolver {
    public class ChanKanResolver : RonResolver {
        public ChanKanResolver(PatternResolver patternResolver) : base(patternResolver) { }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            // Hack: Let the player discard the tile, and then add it back to hand
            var source = incoming.source;
            incoming.source = TileSource.Discard;
            incoming.discardInfo = new DiscardInfo(player, DiscardReason.ChanKan);
            bool result = base.ResolveAction(player, incoming, output);
            incoming.discardInfo = null;
            incoming.source = source;

            return result;
        }
    }
}