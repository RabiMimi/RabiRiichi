using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
    public class StdTestBuilder {
        protected virtual StdPattern V { get; set; }
        protected List<MenLike> groups = new();
        protected List<MenLike> fuuro = new();
        protected List<GameTile> freeTiles = new();
        protected GameTile incoming;
        protected ScoreStorage scores = new();
        protected bool? forceMenzen = null;
        protected readonly RabiMockGame mockGame = new();
        public readonly Mock<Player> currentPlayer;
        public readonly Mock<Player> anotherPlayer;

        protected MenLike Create(string tiles, int fuuroIndex) {
            var t = new Tiles(tiles).ToGameTileList();
            for (int i = 0; i < t.Count; i++) {
                t[i].player = currentPlayer.Object;
                t[i].discardInfo = fuuroIndex == i ? new DiscardInfo(anotherPlayer.Object, DiscardReason.Draw, 0) : null;
            }
            return MenLike.From(t);
        }

        /// <summary>
        /// 创建一个用于测试标准役种的Builder
        /// </summary>
        /// <param name="pattern"></param>
        public StdTestBuilder(StdPattern pattern) {
            V = pattern;
            currentPlayer = new(MockBehavior.Loose, 0, mockGame.Object) {
                CallBase = true
            };
            anotherPlayer = new(MockBehavior.Loose, 1, mockGame.Object) {
                CallBase = true
            };
        }

        /// <summary>
        /// 添加一组面子（副露或暗杠）
        /// </summary>
        /// <param name="tiles">副露/暗杠的面子</param>
        /// <param name="fuuroIndex">其中哪张牌是副露的，最左是0，若无副露则是-1</param>
        public StdTestBuilder AddCalled(string tiles, int fuuroIndex) {
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
        public StdTestBuilder AddAgari(string tiles, string incoming, bool isTsumo = false, TileSource tileSource = TileSource.Wall, DiscardReason reason = DiscardReason.Draw) {
            this.incoming = new GameTile(new Tile(incoming)) {
                player = currentPlayer.Object,
                discardInfo = isTsumo ? null
                    : new DiscardInfo(anotherPlayer.Object, reason, 0),
                source = tileSource,
            };
            var group = new Tiles(tiles).ToGameTileList();
            group.ForEach(g => g.player = currentPlayer.Object);
            freeTiles.AddRange(group.ToList());
            group.Add(this.incoming);
            groups.Add(MenLike.From(group));
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
                called = fuuro,
                menzen = forceMenzen ?? fuuro.All(fuuro => fuuro.IsClose),
            }, incoming, scores);
            if (shouldResolve) {
                Assert.IsTrue(ret, "Expect resolve but failed");
            } else {
                Assert.IsFalse(ret, "Expect fail but resolved");
            }
            return this;
        }

        /// <summary>
        /// 检查是否有给定的计分结果，并将其从scores中移除
        /// </summary>
        /// <param name="type">期望的结果类型</param>
        /// <param name="value">期望的值</param>
        /// <param name="source">期望的来源，默认为传入的resolver</param>
        public StdTestBuilder ExpectScoring(ScoringType type, int value, StdPattern source = null) {
            if (source == null) {
                source = V;
            }
            var s = scores.Find(s => s.Type == type && s.Val == value && s.Source == source);
            Assert.IsNotNull(s, $"Expect scoring {type} {value} but not found");
            scores.Remove(s);
            return this;
        }

        /// <summary>
        /// 检查是否没有更多计分结果了
        /// </summary>
        public StdTestBuilder NoMore() {
            Assert.IsTrue(scores.Count == 0, "Expect no more scores but found");
            return this;
        }

        /// <summary>
        /// 修改游戏选项
        /// </summary>
        public StdTestBuilder WithConfig(Action<GameConfig> action) {
            action(mockGame.Object.config);
            return this;
        }

        /// <summary>
        /// Mock游戏实例
        /// </summary>
        public StdTestBuilder MockGame(Action<RabiMockGame> action) {
            action(mockGame);
            return this;
        }

        /// <summary>
        /// Mock牌山
        /// </summary>
        public StdTestBuilder MockWall(Action<RabiMockWall> action) {
            action(mockGame.wall);
            return this;
        }
    }
}