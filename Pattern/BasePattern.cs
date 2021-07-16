using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public abstract class BasePattern {
        public static GameTiles[] GetTileGroups(Hand hand, GameTile incoming, bool includeGroups) {
            var tileGroups = new GameTiles[128];
            for (int i = 0; i < tileGroups.Length; i++) {
                tileGroups[i] = new GameTiles();
            }
            var tiles = (includeGroups
                ? hand.hand.Concat(hand.groups.SelectMany(gr => gr))
                : hand.hand).ToList();
            if (incoming != null) {
                tiles.Add(incoming);
            }
            foreach (var tile in tiles) {
                int index = tile.tile.NoDoraVal;
                tileGroups[index].Add(tile);
            }
            return tileGroups;
        }

        public abstract bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output);
    }
}
