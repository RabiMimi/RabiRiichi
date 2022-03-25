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
                actionCenter = new ScenarioActionCenter(),
                seed = 114514,
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

            /// <summary> 设置随机种子，默认为114514 </summary>
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

        #region GameState
        public class GameStateBuilder {
            /// <summary> 第几轮 </summary>
            public int round = 0;
            /// <summary> 庄家ID </summary>
            public int dealer = 0;
            /// <summary> 本场 </summary>
            public int honba = 0;
            /// <summary> 立直棒数量 </summary>
            public int riichiStick = 0;

            public GameStateBuilder SetRound(Wind bakaze, Wind dealer, int honba = 0) {
                this.round = (int)bakaze;
                this.dealer = (int)dealer;
                this.honba = honba;
                return this;
            }

            public GameStateBuilder SetRiichiStick(int riichiStick) {
                this.riichiStick = riichiStick;
                return this;
            }

            public GameInfo Setup(GameInfo info) {
                info.round = round;
                info.dealer = dealer;
                info.honba = honba;
                info.riichiStick = riichiStick;
                return info;
            }
        }
        private readonly GameStateBuilder gameStateBuilder;

        public ScenarioBuilder WithState(Action<GameStateBuilder> action) {
            action(gameStateBuilder);
            return this;
        }

        public ScenarioBuilder SetRound(Wind bakaze, Wind dealer, int honba = 0) {
            gameStateBuilder.SetRound(bakaze, dealer, honba);
            return this;
        }

        #endregion

        #region Player
        public class PlayerHandBuilder {
            public class MenInfo {
                public readonly Tiles tiles;
                public readonly int fuuroIndex;
                public readonly int fromPlayer;
                public bool IsClosed => fuuroIndex == -1;
                public readonly DiscardReason reason;

                public MenInfo(Tiles tiles, int fuuroIndex, int fromPlayer, DiscardReason reason) {
                    this.tiles = tiles;
                    this.fuuroIndex = fuuroIndex;
                    this.fromPlayer = fromPlayer;
                    this.reason = reason;
                }

                public MenLike Create(List<GameTile> tiles, Player[] players) {
                    if (!IsClosed) {
                        tiles[fuuroIndex].discardInfo = new DiscardInfo(players[fromPlayer], reason);
                    }
                    return MenLike.From(tiles);
                }
            }
            private int? points;
            private bool? menzen;
            public Tile? riichiTile;
            private bool? wRiichi;
            public Tiles freeTiles = new();
            public Tiles discarded = new();
            public Tiles blockedDiscarded = new();
            public int discardedNum = 5;
            public List<MenInfo> called = new();
            public Player player;
            private bool? isTempFuriten;
            private bool? isRiichiFuriten;
            private bool? isDiscardFuriten;
            public bool? ippatsu;
            public int TotalTilesInHand => freeTiles.Count + called.Sum(x => Math.Min(3, x.tiles.Count));
            public int handSize;

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

            /// <summary> 设置是否是一发，默认不是 </summary>
            public PlayerHandBuilder SetIppatsu(bool isIppatsu) {
                this.ippatsu = isIppatsu;
                return this;
            }

            /// <summary> 设置立直牌，默认不立直 </summary>
            public PlayerHandBuilder SetRiichiTile(Tile riichiTile, bool? wRiichi = null) {
                this.riichiTile = riichiTile;
                if (wRiichi.HasValue) {
                    this.wRiichi = wRiichi.Value;
                }
                return this;
            }
            public PlayerHandBuilder SetRiichiTile(string riichiTile, bool? wRiichi = null)
                => SetRiichiTile(new Tile(riichiTile), wRiichi);

            /// <summary>
            /// 设置舍牌，默认为5张
            /// 若指定的舍牌数量不够则用别的牌填充
            /// </summary>
            /// <param name="count">舍牌数量</param>
            /// <param name="discarded">舍牌</param>
            /// <param name="reservedDiscarded">自动填充时，禁止出现在舍牌里的牌</param>
            public PlayerHandBuilder SetDiscarded(int count, Tiles discarded = null, Tiles blocked = null) {
                if (discarded != null) {
                    this.discarded = discarded;
                }
                if (blocked != null) {
                    this.blockedDiscarded = blocked;
                }
                this.discardedNum = count;
                return this;
            }

            /// <summary> 设置手牌 </summary>
            public PlayerHandBuilder SetFreeTiles(string freeTiles) {
                this.freeTiles = new Tiles(freeTiles);
                return this;
            }

            /// <summary> 添加一个面子 </summary>
            public PlayerHandBuilder AddCalled(string called, int fuuroIndex = -1, int fromPlayer = -1, DiscardReason reason = DiscardReason.None) {
                var info = new MenInfo(new Tiles(called), fuuroIndex, fromPlayer, reason);
                this.called.Add(info);
                return this;
            }

            public Player Setup(Player player, int handSize = Game.HAND_SIZE) {
                // Validate
                if (!riichiTile.HasValue) {
                    if (wRiichi == true) {
                        throw new Exception($"P{player.id}: Cannot wRiichi without riichiTile");
                    }
                    if (ippatsu == true) {
                        throw new Exception($"P{player.id}: Cannot ippatsu without riichiTile");
                    }
                }
                int totalHand = TotalTilesInHand;
                if (totalHand > handSize) {
                    throw new Exception($"P{player.id}: Too many tiles in hand");
                }
                if (freeTiles.Count > 0) {
                    if (totalHand != handSize) {
                        throw new Exception($"P{player.id}: Not enough tiles in hand");
                    }
                }
                if (discarded.Count + (riichiTile.HasValue ? 1 : 0) > discardedNum) {
                    throw new Exception($"P{player.id}: Too many discarded tiles");
                }
                if (discarded.Any(x => blockedDiscarded.Contains(x))) {
                    throw new Exception($"P{player.id}: Cannot discard reserved tile");
                }
                // Set data
                this.player = player;
                this.handSize = handSize;
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
                if (ippatsu.HasValue) {
                    player.hand.ippatsu = ippatsu.Value;
                }
                if (wRiichi.HasValue) {
                    player.hand.wRiichi = wRiichi.Value;
                }
                player.hand.jun = discardedNum;
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
            private readonly List<PlayerHandBuilder> playerBuilders;
            private int revealedDoraNum = 1;

            public WallBuilder(IEnumerable<PlayerHandBuilder> players) {
                this.playerBuilders = players.ToList();
            }

            /// <summary> 保留一些牌。这些牌会被放在牌山最前。 </summary>
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

            /// <summary> </summary>
            public WallBuilder AddPlayer(PlayerHandBuilder builder) {
                playerBuilders.Add(builder);
                return this;
            }

            public Wall Setup(Wall wall) {
                if (playerBuilders.Any(builder => builder.player == null)) {
                    throw new InvalidOperationException("Must set up PlayerHandBuilder before setting up wall.");
                }
                var players = playerBuilders.Select(builder => builder.player).ToArray();
                var allTiles = wall.doras.Concat(wall.uradoras).Concat(wall.rinshan).Concat(wall.remaining).ToList();
                wall.revealedDoraCount = revealedDoraNum;

                GameTile Draw(Tile tile) {
                    var index = allTiles.FindIndex(t => t.tile == tile);
                    if (index < 0) {
                        throw new InvalidOperationException($"Tile {tile} not found in wall.");
                    }
                    var gameTile = allTiles[index];
                    allTiles.RemoveAt(index);
                    return gameTile;
                }

                GameTile DrawNext(IEnumerable<Tile> blocked = null) {
                    for (int i = 0; i < allTiles.Count; i++) {
                        var tile = allTiles[i];
                        if (blocked == null || !blocked.Contains(tile.tile)) {
                            var gameTile = allTiles[i];
                            allTiles.RemoveAt(i);
                            return gameTile;
                        }
                    }
                    throw new InvalidOperationException("No tile found in wall.");
                }

                // Draw tiles for wall
                wall.doras.Clear();
                wall.uradoras.Clear();
                wall.rinshan.Clear();
                wall.remaining.Clear();
                for (int i = 0; i < doras.Count; i++) {
                    wall.doras.Add(Draw(doras[i]));
                }
                for (int i = 0; i < uradoras.Count; i++) {
                    wall.uradoras.Add(Draw(uradoras[i]));
                }
                for (int i = 0; i < rinshan.Count; i++) {
                    wall.rinshan.Add(Draw(rinshan[i]));
                }
                for (int i = 0; i < reserved.Count; i++) {
                    wall.remaining.Add(Draw(reserved[i]));
                }

                // Try draw tiles for each player
                foreach (var playerBuilder in playerBuilders) {
                    var hand = playerBuilder.player.hand;
                    // Reset hand
                    hand.freeTiles.Clear();
                    hand.discarded.Clear();
                    hand.called.Clear();
                    hand.riichiTile = null;
                    hand.agariTile = null;

                    // Draw free tiles
                    foreach (var tile in playerBuilder.freeTiles) {
                        hand.Add(Draw(tile));
                    }
                    // Draw called tiles
                    foreach (var tiles in playerBuilder.called) {
                        var gameTiles = tiles.tiles.Select(Draw).ToList();
                        var menLike = tiles.Create(gameTiles, players);
                        if (menLike is Shun shun) {
                            hand.AddChii(shun);
                        } else if (menLike is Kou kou) {
                            hand.AddPon(kou);
                        } else if (menLike is Kan kan) {
                            hand.AddKan(kan);
                        } else {
                            throw new InvalidOperationException($"Unknown called type: {menLike.GetType()}");
                        }
                    }
                    // Draw discarded tiles
                    foreach (var tile in playerBuilder.discarded) {
                        hand.Play(Draw(tile), DiscardReason.Draw);
                    }
                    // Draw riichi tile
                    if (playerBuilder.riichiTile.HasValue) {
                        hand.riichiTile = Draw(playerBuilder.riichiTile.Value);
                    }
                }

                // Fill the rest of player hand
                foreach (var playerBuilder in playerBuilders) {
                    var hand = playerBuilder.player.hand;
                    int countLeft = playerBuilder.handSize - playerBuilder.TotalTilesInHand;
                    for (int i = 0; i < countLeft; i++) {
                        hand.Add(DrawNext(playerBuilder.blockedDiscarded.Concat(reserved)));
                    }
                }

                // Fill the rest of the tiles in wall
                for (int i = doras.Count; i < Wall.NUM_DORA; i++) {
                    wall.doras.Add(DrawNext());
                }
                for (int i = uradoras.Count; i < Wall.NUM_DORA; i++) {
                    wall.uradoras.Add(DrawNext());
                }
                for (int i = rinshan.Count; i < Wall.NUM_RINSHAN; i++) {
                    wall.rinshan.Add(DrawNext());
                }
                wall.remaining.InsertRange(0, allTiles);
                return wall;
            }
        }
        private readonly WallBuilder wallBuilder;

        public ScenarioBuilder WithWall(Action<WallBuilder> action) {
            action(wallBuilder);
            return this;
        }
        #endregion

        private Game game;

        public ScenarioBuilder(int playerCount) {
            configBuilder = new GameConfigBuilder(playerCount);
            playerHandBuilders = new PlayerHandBuilder[playerCount];
            for (var i = 0; i < playerCount; i++) {
                playerHandBuilders[i] = new();
            }
            gameStateBuilder = new GameStateBuilder();
            wallBuilder = new WallBuilder(playerHandBuilders);
        }

        public ScenarioBuilder Setup() {
            game = new Game(configBuilder.Build());
            gameStateBuilder.Setup(game.info);
            for (int i = 0; i < playerHandBuilders.Length; i++) {
                var player = game.GetPlayer(i);
                playerHandBuilders[i].Setup(player,
                    game.IsFirstJun && player.IsDealer ? Game.HAND_SIZE + 1 : Game.HAND_SIZE);
            }
            wallBuilder.Setup(game.wall);
            return this;
        }
    }
}