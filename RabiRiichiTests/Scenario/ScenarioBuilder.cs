using RabiRiichi.Core;
using System;
using System.Collections.Generic;

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

            public GameConfigBuilder OverwriteConfig(GameConfig config) {
                config.setup = this.config.setup;
                config.actionCenter = this.config.actionCenter;
                this.config = config;
                return this;
            }

            public GameConfigBuilder SetPlayerCount(int playerCount) {
                config.playerCount = playerCount;
                return this;
            }

            public GameConfigBuilder SetMinHan(int minHan) {
                config.minHan = minHan;
                return this;
            }

            public GameConfigBuilder SetInitialPoints(int initialPoints) {
                config.initialPoints = initialPoints;
                return this;
            }

            public GameConfigBuilder SetRiichiPoints(int riichiPoints) {
                config.riichiPoints = riichiPoints;
                return this;
            }

            public GameConfigBuilder SetHonbaPoints(int honbaPoints) {
                config.honbaPoints = honbaPoints;
                return this;
            }

            public GameConfigBuilder SetFinishPoints(int finishPoints) {
                config.finishPoints = finishPoints;
                return this;
            }

            public GameConfigBuilder SetAllowKuitan(bool allowKuitan) {
                config.allowKuitan = allowKuitan;
                return this;
            }

            public GameConfigBuilder SetTotalRound(int totalRound) {
                config.totalRound = totalRound;
                return this;
            }

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

            public PlayerHandBuilder SetPoints(int points) {
                this.points = points;
                return this;
            }

            public PlayerHandBuilder SetMenzen(bool menzen) {
                this.menzen = menzen;
                return this;
            }

            public PlayerHandBuilder SetRiichiTile(Tile riichiTile, bool? wRiichi = null) {
                this.riichiTile = riichiTile;
                this.wRiichi = wRiichi;
                return this;
            }

            public PlayerHandBuilder SetWRiichi(bool wRiichi) {
                this.wRiichi = wRiichi;
                return this;
            }

            public PlayerHandBuilder SetFreeTiles(string freeTiles) {
                this.freeTiles = new Tiles(freeTiles);
                return this;
            }

            public PlayerHandBuilder SetCalled(string called, int fuuroIndex = -1, int fromPlayer = -1) {
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

        public ScenarioBuilder(int playerCount) {
            configBuilder = new GameConfigBuilder(playerCount);
            playerHandBuilders = new PlayerHandBuilder[playerCount];
            for (var i = 0; i < playerCount; i++) {
                playerHandBuilders[i] = new();
            }
        }
    }
}