using System.Collections.Generic;
namespace RabiRiichi.Riichi {
    /// <summary> 将相同的牌放入同一个bucket </summary>
    public class GameTileBucket {
        private GameTiles[,] buckets = new GameTiles[5, 10];
        public GameTileBucket() {
            for (int i = 1; i < 5; i++) {
                for (int j = 1; j < 10; j++) {
                    buckets[i, j] = new GameTiles();
                }
            }
        }
        public GameTileBucket(IEnumerable<GameTile> tiles) : this() {
            foreach (var tile in tiles) {
                Add(tile);
            }
        }

        public GameTileBucket Add(GameTile tile) {
            buckets[(int)tile.tile.Gr, tile.tile.Num].Add(tile);
            return this;
        }

        public GameTiles GetBucket(Group group, int num) {
            return buckets[(int)group, num];
        }

        public GameTiles GetBucket(Tile tile) {
            return GetBucket(tile.Gr, tile.Num);
        }

        public IEnumerable<(GameTiles, int)> GetGroup(Group group) {
            for (int i = 1; i < 10; i++) {
                yield return (GetBucket(group, i), i);
            }
        }

        public IEnumerable<(IEnumerable<(GameTiles, int)>, Group)> GetAll() {
            for (int i = 1; i <= 4; i++) {
                Group gr = (Group)i;
                yield return (GetGroup(gr), gr);
            }
        }

        public IEnumerable<GameTiles> GetAllBuckets(bool skipEmpty = true) {
            foreach (var (group, gr) in GetAll()) {
                foreach (var (bucket, _) in group) {
                    if (skipEmpty && bucket.Count == 0)
                        continue;
                    yield return bucket;
                }
            }
        }
    }
}
