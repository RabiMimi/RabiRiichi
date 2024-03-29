﻿using System.Collections.Generic;
namespace RabiRiichi.Core {
    /// <summary> 将相同的牌放入同一个bucket </summary>
    public class GameTileBucket {
        private readonly List<GameTile>[,] buckets = new List<GameTile>[5, 10];
        public GameTileBucket() {
            for (int i = 1; i < 5; i++) {
                for (int j = 1; j < 10; j++) {
                    buckets[i, j] = new List<GameTile>();
                }
            }
        }
        public GameTileBucket(IEnumerable<GameTile> tiles) : this() {
            foreach (var tile in tiles) {
                Add(tile);
            }
        }

        public GameTileBucket Add(GameTile tile) {
            buckets[(int)tile.tile.Suit, tile.tile.Num].Add(tile);
            return this;
        }

        public List<GameTile> GetBucket(TileSuit group, int num) {
            return buckets[(int)group, num];
        }

        public List<GameTile> GetBucket(Tile tile) {
            return GetBucket(tile.Suit, tile.Num);
        }

        public IEnumerable<(List<GameTile>, int)> GetGroup(TileSuit group) {
            for (int i = 1; i < 10; i++) {
                yield return (GetBucket(group, i), i);
            }
        }

        public IEnumerable<(IEnumerable<(List<GameTile>, int)>, TileSuit)> GetAll() {
            for (int i = 1; i <= 4; i++) {
                TileSuit gr = (TileSuit)i;
                yield return (GetGroup(gr), gr);
            }
        }

        public IEnumerable<List<GameTile>> GetAllBuckets(bool skipEmpty = true) {
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
