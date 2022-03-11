using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichiTests.Pattern {
    public class StdTestBuilder {
        protected virtual StdPattern V { get; set; }
        protected List<MenOrJantou> groups = new();
        protected List<MenOrJantou> fuuro = new();
        protected GameTiles freeTiles = new();
        protected GameTile incoming;
        protected Scorings scorings = new();
        protected bool? forceMenzen = null;
        public Mock<Player> currentPlayer = new(MockBehavior.Default, 0, null);
        public Mock<Player> anotherPlayer = new(MockBehavior.Default, 1, null);

        protected MenOrJantou Create(string tiles, int fuuroIndex) {
            var t = new GameTiles(new Tiles(tiles));
            for (int i = 0; i < t.Count; i++) {
                t[i].player = currentPlayer.Object;
                t[i].fromPlayer = fuuroIndex == i ? anotherPlayer.Object : null;
            }
            return MenOrJantou.From(t);
        }

        /// <summary>
        /// 创建一个用于测试标准役种的Builder
        /// </summary>
        /// <param name="pattern"></param>
        public StdTestBuilder(StdPattern pattern) {
            V = pattern;
            currentPlayer.Setup(p => p.IsYaku(It.IsAny<Tile>())).Returns(false);
            anotherPlayer.Setup(p => p.IsYaku(It.IsAny<Tile>())).Returns(false);
        }

        /// <summary>
        /// 添加一组副露
        /// </summary>
        /// <param name="tiles">副露的面子</param>
        /// <param name="fuuroIndex">其中哪张牌是副露的，最左是0</param>
        public StdTestBuilder AddFuuro(string tiles, int fuuroIndex) {
            var gameTiles = Create(tiles, fuuroIndex);
            fuuro.Add(gameTiles);
            groups.Add(gameTiles);
            return this;
        }

        /// <summary>
        /// 强制标注门清情况
        /// </summary>
        public StdTestBuilder ForceMenzen(bool menzen) {
            forceMenzen = menzen;
            return this;
        }

        /// <summary>
        /// 添加一组手牌
        /// </summary>
        /// <param name="tiles">手牌的面子</param>
        public StdTestBuilder AddFree(string tiles) {
            var gameTiles = Create(tiles, -1);
            groups.Add(gameTiles);
            freeTiles.AddRange(gameTiles);
            return this;
        }

        /// <summary>
        /// 添加和牌的组合
        /// </summary>
        /// <param name="tiles">和牌的面子/雀头</param>
        /// <param name="incoming">和了牌</param>
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

        /// <summary>
        /// 使用给定的StdPatternResolver进行解析
        /// </summary>
        /// <param name="shouldResolve">是否期望成功解析</param>
        public StdTestBuilder Resolve(bool shouldResolve) {
            bool ret = V.Resolve(groups, new Hand {
                player = currentPlayer.Object,
                freeTiles = freeTiles,
                fuuro = fuuro,
                menzen = forceMenzen ?? fuuro.Count == 0,
            }, incoming, scorings);
            if (shouldResolve) {
                Assert.IsTrue(ret, "Expect resolve but failed");
            } else {
                Assert.IsFalse(ret, "Expect fail but resolved");
            }
            return this;
        }

        /// <summary>
        /// 检查是否有给定的计分结果，并将其从scorings中移除
        /// </summary>
        /// <param name="type">期望的结果类型</param>
        /// <param name="value">期望的值</param>
        /// <param name="source">期望的来源，默认为传入的resolver</param>
        public StdTestBuilder ExpectScoring(ScoringType type, int value, StdPattern source = null) {
            if (source == null) {
                source = V;
            }
            var s = scorings.Find(s => s.Type == type && s.Val == value && s.Source == source);
            Assert.IsNotNull(s, $"Expect scoring {type} {value} but not found");
            scorings.Remove(s);
            return this;
        }

        public StdTestBuilder NoMore() {
            Assert.IsTrue(scorings.Count == 0, "Expect no more scorings but found");
            return this;
        }
    }
}