using RabiRiichi.Core;
using System;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichiTests.Scenario {
    public class ScenarioBuilder {
        #region GameConfig
        public class GameConfigBuilder {
            private GameConfig config = new() {
                setup = new ScenarioSetup(),
                actionCenter = new ScenarioActionCenter()
            };
            public ScenarioActionCenter actionCenter => (ScenarioActionCenter)config.actionCenter;

            public GameConfigBuilder(int playerCount) {
                config.playerCount = playerCount;
            }

            /// <summary> 覆盖当前config，但保留setup和actionCenter </summary>
            public GameConfigBuilder OverwriteConfig(GameConfig config) {
                config.setup = this.config.setup;
                config.actionCenter = this.config.actionCenter;
                this.config = config;
                return this;
            }

            /// <summary> 设置番缚，默认为1 </summary>
            public GameConfigBuilder SetMinHan(int minHan) {
                config.minHan = minHan;
                return this;
            }

            /// <summary> 设置初始点，默认为25000 </summary>
            public GameConfigBuilder SetInitialPoints(int initialPoints) {
                config.initialPoints = initialPoints;
                return this;
            }

            /// <summary> 设置立直棒点数，默认为1000 </summary>
            public GameConfigBuilder SetRiichiPoints(int riichiPoints) {
                config.riichiPoints = riichiPoints;
                return this;
            }

            /// <summary> 设置本场棒总点数，默认为300 </summary>
            public GameConfigBuilder SetHonbaPoints(int honbaPoints) {
                config.honbaPoints = honbaPoints;
                return this;
            }

            /// <summary> 设置终局点数，默认为30000 </summary>
            public GameConfigBuilder SetFinishPoints(int finishPoints) {
                config.finishPoints = finishPoints;
                return this;
            }

            /// <summary> 是否允许食断，默认允许 </summary>
            public GameConfigBuilder SetAllowKuitan(bool allowKuitan) {
                config.allowKuitan = allowKuitan;
                return this;
            }

            /// <summary> 总局数，默认东风战 </summary>
            public GameConfigBuilder SetTotalRound(int totalRound) {
                config.totalRound = totalRound;
                return this;
            }

            /// <summary> 设置随机种子，默认为当前时间 </summary>
            public GameConfigBuilder SetSeed(int? seed) {
                config.seed = seed;
                return this;
            }

            public GameConfig Build() {
                return config;
            }
        }

        private readonly GameConfigBuilder configBuilder;
        private ScenarioActionCenter actionCenter => configBuilder.actionCenter;

        public ScenarioBuilder WithConfig(Action<GameConfigBuilder> action) {
            action(configBuilder);
            return this;
        }
        #endregion

        #region Player
        public class PlayerHandBuilder {
            private class MenInfo {
                public readonly Tiles tiles;
                public readonly int fuuroIndex;
                public readonly int fromPlayer;
                public bool IsClosed => fuuroIndex == -1;

                public MenInfo(Tiles tiles, int fuuroIndex, int fromPlayer) {
                    this.tiles = tiles;
                    this.fuuroIndex = fuuroIndex;
                    this.fromPlayer = fromPlayer;
                }
            }
            private int? points;
            private bool? menzen;
            private Tile? riichiTile;
            private bool? wRiichi;
            private Tiles freeTiles;
            private List<MenInfo> called = new();
            private bool? isTempFuriten;
            private bool? isRiichiFuriten;
            private bool? isDiscardFuriten;

            /// <summary> 设置点数 </summary>
            public PlayerHandBuilder SetPoints(int points) {
                this.points = points;
                return this;
            }

            /// <summary> 设置是否门清，默认按照是否有副露来计算 </summary>
            public PlayerHandBuilder SetMenzen(bool menzen) {
                this.menzen = menzen;
                return this;
            }

            /// <summary> 设置是否立直振听，默认没有 </summary>
            public PlayerHandBuilder SetRiichiFuriten(bool isFuriten) {
                this.isRiichiFuriten = isFuriten;
                return this;
            }

            /// <summary> 设置是否同巡振听，默认没有 </summary>
            public PlayerHandBuilder SetTempFuriten(bool isFuriten) {
                this.isTempFuriten = isFuriten;
                return this;
            }

            /// <summary> 设置是否舍牌振听，默认没有 </summary>
            public PlayerHandBuilder SetDiscardFuriten(bool isFuriten) {
                this.isDiscardFuriten = isFuriten;
                return this;
            }

            /// <summary> 设置立直牌，默认不立直 </summary>
            public PlayerHandBuilder SetRiichiTile(Tile riichiTile, bool? wRiichi = null) {
                this.riichiTile = riichiTile;
                this.wRiichi = wRiichi;
                return this;
            }

            /// <summary> 设置手牌 </summary>
            public PlayerHandBuilder SetFreeTiles(string freeTiles) {
                this.freeTiles = new Tiles(freeTiles);
                return this;
            }

            /// <summary> 添加一个面子 </summary>
            public PlayerHandBuilder AddCalled(string called, int fuuroIndex = -1, int fromPlayer = -1) {
                if (this.called == null) {
                    this.called = new List<MenInfo>();
                }
                var info = new MenInfo(new Tiles(called), fuuroIndex, fromPlayer);
                this.called.Add(info);
                return this;
            }

            public Player Build(Player player) {
                if (points.HasValue) {
                    player.points = points.Value;
                }
                if (menzen.HasValue) {
                    player.hand.menzen = menzen.Value;
                } else {
                    player.hand.menzen = called?.Any(x => !x.IsClosed) ?? false;
                }
                if (isTempFuriten.HasValue) {
                    player.hand.isTempFuriten = isTempFuriten.Value;
                }
                if (isRiichiFuriten.HasValue) {
                    player.hand.isRiichiFuriten = isRiichiFuriten.Value;
                }
                if (isDiscardFuriten.HasValue) {
                    player.hand.isDiscardFuriten = isDiscardFuriten.Value;
                }
                if (riichiTile.HasValue) {
                    // TODO: Create a wall for testing
                }
                if (wRiichi.HasValue) {
                    player.hand.wRiichi = wRiichi.Value;
                }
                if (freeTiles != null) {
                    // player.hand.freeTiles = freeTiles;
                }
                if (called != null) {
                    // player.hand.called = called;
                }
                return player;
            }
        }

        private readonly PlayerHandBuilder[] playerHandBuilders;

        public ScenarioBuilder WithPlayer(int playerId, Action<PlayerHandBuilder> action) {
            action(playerHandBuilders[playerId]);
            return this;
        }
        #endregion

        #region Wall
        public class WallBuilder {
            private readonly Tiles reserved = new();
            private readonly Tiles doras = new();
            private readonly Tiles uradoras = new();
            private readonly Tiles rinshan = new();
            private int revealedDoraNum = 1;
            private int[] riverTileNum;

            public WallBuilder(int playerCount) {
                riverTileNum = new int[playerCount];
                for (var i = 0; i < playerCount; i++) {
                    riverTileNum[i] = 2;
                }
            }

            /// <summary> 保留一些牌。这些牌不会被用于填充牌河。 </summary>
            public WallBuilder Reserve(IEnumerable<Tile> tiles) {
                reserved.AddRange(tiles);
                return this;
            }
            public WallBuilder Reserve(string tiles) => Reserve(new Tiles(tiles));
            public WallBuilder Reserve(Tile tile) => Reserve(Enumerable.Repeat(tile, 1));

            /// <summary> 添加宝牌。第一个宝牌的下标为0。 </summary>
            public WallBuilder AddDoras(IEnumerable<Tile> tiles) {
                doras.AddRange(tiles);
                return this;
            }
            public WallBuilder AddDoras(string tiles) => AddDoras(new Tiles(tiles));
            public WallBuilder AddDoras(Tile tile) => AddDoras(Enumerable.Repeat(tile, 1));

            /// <summary> 添加里宝牌。第一个里宝牌的下标为0。 </summary>
            public WallBuilder AddUradoras(IEnumerable<Tile> tiles) {
                uradoras.AddRange(tiles);
                return this;
            }
            public WallBuilder AddUradoras(string tiles) => AddUradoras(new Tiles(tiles));
            public WallBuilder AddUradoras(Tile tile) => AddUradoras(Enumerable.Repeat(tile, 1));

            /// <summary> 添加岭上牌。最后一张岭上牌的下标为0。 </summary>
            public WallBuilder AddRinshan(IEnumerable<Tile> tiles) {
                rinshan.AddRange(tiles);
                return this;
            }
            public WallBuilder AddRinshan(string tiles) => AddRinshan(new Tiles(tiles));
            public WallBuilder AddRinshan(Tile tile) => AddRinshan(Enumerable.Repeat(tile, 1));

            /// <summary> 设置有多少Dora已经翻开了，默认为1。 </summary>
            public WallBuilder SetRevealedDoraCount(int num) {
                revealedDoraNum = num;
                return this;
            }

            /// <summary> 设置每个玩家牌河里的牌数，默认为2 </summary>
            public WallBuilder SetRiverTileNum(params int[] num) {
                if (num.Length != riverTileNum.Length) {
                    throw new ArgumentException("River count length must be equal to player count.");
                }
                riverTileNum = num;
                return this;
            }
        }
        #endregion
        public ScenarioBuilder(int playerCount) {
            configBuilder = new GameConfigBuilder(playerCount);
            playerHandBuilders = new PlayerHandBuilder[playerCount];
            for (var i = 0; i < playerCount; i++) {
                playerHandBuilders[i] = new();
            }
        }
    }
}