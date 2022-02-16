using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using Moq;

namespace RabiRiichiTests.Pattern {
    public class StdTestBuilder {
        protected virtual StdPattern V { get; set; }
        protected List<MenOrJantou> groups = new();
        protected List<MenOrJantou> fuuro = new();
        protected GameTiles freeTiles = new();
        protected GameTile incoming;
        protected Scorings scorings = new();
        public Mock<Player> currentPlayer = new Mock<Player>(MockBehavior.Default, 0, null);
        public Mock<Player> anotherPlayer = new Mock<Player>(MockBehavior.Default, 1, null);

        protected MenOrJantou Create(string tiles, int fuuroIndex) {
            var t = new GameTiles(new Tiles(tiles));
            for (int i = 0; i < t.Count; i++) {
                t[i].player = currentPlayer.Object;
                t[i].fromPlayer = fuuroIndex == i ? anotherPlayer.Object : null;
            }
            return MenOrJantou.From(t);
        }

        public StdTestBuilder(StdPattern pattern) {
            V = pattern;
            currentPlayer.Setup(p => p.IsYaku(It.IsAny<Tile>())).Returns(false);
            anotherPlayer.Setup(p => p.IsYaku(It.IsAny<Tile>())).Returns(false);
        }

        /// <summary>
        /// 添加一组副露
        /// <param name="tiles">副露的面子</param>
        /// <param name="fuuroIndex">其中哪张牌是副露的，最左是0</param>
        /// </summary>
        public StdTestBuilder AddFuuro(string tiles, int fuuroIndex) {
            var gameTiles = Create(tiles, fuuroIndex);
            fuuro.Add(gameTiles);
            groups.Add(gameTiles);
            return this;
        }

        public StdTestBuilder AddFreeGroup(string tiles) {
            var gameTiles = Create(tiles, -1);
            groups.Add(gameTiles);
            freeTiles.AddRange(gameTiles);
            return this;
        }

        public StdTestBuilder AddAgari(string tiles, string incoming) {
            this.incoming = new GameTile(new Tile(incoming)) {
                player = currentPlayer.Object,
                fromPlayer = anotherPlayer.Object
            };
            var group = new GameTiles(new Tiles(tiles));
            group.ForEach(g => g.player = currentPlayer.Object);
            freeTiles.AddRange(group.ToList());
            group.Add(this.incoming);
            groups.Add(MenOrJantou.From(group));
            return this;
        }

        public StdTestBuilder Resolve(bool shouldResolve) {
            bool ret = V.Resolve(groups, new Hand {
                player = currentPlayer.Object,
                freeTiles = freeTiles,
                fuuro = fuuro,
            }, incoming, scorings);
            if (shouldResolve) {
                Assert.IsTrue(ret, "Expect resolve but failed");
            } else {
                Assert.IsFalse(ret, "Expect fail but resolved");
            }
            return this;
        }

        public StdTestBuilder ExpectScoring(ScoringType type, int value, StdPattern source = null) {
            if (source == null) {
                source = V;
            }
            var s = scorings.Find(s => s.Type == type && s.Val == value && s.Source == source);
            Assert.IsNotNull(s, $"Expect scoring {type} {value} but not found");
            scorings.Remove(s);
            return this;
        }
    }
}