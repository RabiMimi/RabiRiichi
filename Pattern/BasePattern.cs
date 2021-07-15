using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public abstract class BasePattern {
        public abstract bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output);
    }
}
