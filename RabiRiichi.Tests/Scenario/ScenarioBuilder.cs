using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace RabiRiichi.Tests.Scenario {
    public class Scenario {
        private Task runToEnd;
        private readonly Game game;
        private readonly int startPlayerId;
        private readonly ScenarioActionCenter actionCenter;
        private readonly List<Predicate<EventBase>> eventMatchers = new();
        private readonly List<Predicate<EventBase>> noEventMatchers = new();
        private readonly List<EventBase> events = new();
        private bool sanityCheckOnInquiry = true;

        /// <summary> 检查当前游戏状态是否合法 </summary>
        private void GameSanityCheck(ScenarioInquiryMatcher inquiry) {
            if (!sanityCheckOnInquiry) {
                return;
            }
            // Player
            foreach (var player in game.players) {
                int expectedCount = Game.HAND_SIZE;
                var action = (PlayTileAction)inquiry.inquiry.GetByPlayerId(player.id)?.actions.Find(action => action is PlayTileAction);
                if (action?.options.All(x => player.hand.freeTiles.Contains((x as ChooseTileActionOption).tile)) == true) {
                    expectedCount++;
                }
                Assert.AreEqual(expectedCount, player.hand.Count, $"Player {player.id} hand count is not {Game.HAND_SIZE}");
                if (player.hand.wRiichi) {
                    Assert.IsTrue(player.hand.riichi, $"Player {player.id} is wRiichi but not riichi");
                }
            }
            // Wall
            Assert.IsTrue(game.wall.NumRemaining >= 0, $"Wall remaining is {game.wall.NumRemaining} < 0");
            Assert.AreEqual(Wall.NUM_DORA, game.wall.uradoras.Count, $"Wall uradoras count is {game.wall.uradoras.Count} != {Wall.NUM_DORA}");
            Assert.AreEqual(Wall.NUM_DORA, game.wall.doras.Count, $"Wall doras count is {game.wall.doras.Count} != {Wall.NUM_DORA}");
            Assert.IsTrue(game.wall.rinshan.Count <= Wall.NUM_RINSHAN, $"Wall rinshan count is {game.wall.rinshan.Count} > {Wall.NUM_RINSHAN}");
        }

        public Scenario(Game game, int startPlayerId) {
            this.game = game;
            this.startPlayerId = startPlayerId;
            actionCenter = (ScenarioActionCenter)game.config.actionCenter;
        }

        /// <summary> 启用或禁用游戏询问玩家时的状态检查。默认启用 </summary>
        public Scenario SanityCheckOnInquiry(bool enabled) {
            sanityCheckOnInquiry = enabled;
            return this;
        }

        /// <summary> 获取游戏实例 </summary>
        public Scenario WithGame(Action<Game> action) {
            action(game);
            return this;
        }

        /// <summary> 获取信息实例 </summary>
        public Scenario WithGameInfo(Action<GameInfo> action) {
            action(game.info);
            return this;
        }

        /// <summary> 获取牌山实例 </summary>
        public Scenario WithWall(Action<Wall> action) {
            action(game.wall);
            return this;
        }

        /// <summary> 获取玩家实例 </summary>
        public Scenario WithPlayer(int playerId, Action<Player> action) {
            action(game.GetPlayer(playerId));
            return this;
        }

        /// <summary> 以玩家playerId的回合开始游戏 </summary>
        public Scenario Start() {
            // Clear events, so that the initial event will not be processed
            game.mainQueue.ClearEvents();
            game.eventBus.Subscribe<EventBase>((ev) => {
                events.Add(ev);
                return Task.CompletedTask;
            }, EventPriority.Broadcast);
            if (game.IsFirstJun && game.Dealer.id == startPlayerId) {
                // First jun of dealer
                game.mainQueue.Queue(new IncreaseJunEvent(game.initialEvent, startPlayerId));
                game.mainQueue.Queue(new DealerFirstTurnEvent(game.initialEvent, startPlayerId, game.Dealer.hand.freeTiles[^1]));
            } else {
                // Otherwise
                game.mainQueue.Queue(new NextPlayerEvent(game.initialEvent, game.PrevPlayerId(startPlayerId)));
            }
            runToEnd = game.Start().ContinueWith((e) => {
                if (e.IsFaulted) {
                    actionCenter.ForceFail(e.Exception);
                } else if (e.IsCanceled) {
                    actionCenter.ForceFail(new Exception("Game cancelled"));
                } else {
                    actionCenter.ForceCancel();
                }
            });
            return this;
        }

        /// <summary> 测试有对应的事件按顺序发生 </summary>
        public Scenario AssertEvent<T>(Predicate<T> predicate) where T : EventBase {
            eventMatchers.Add(
                ev => ev is T tEv && predicate(tEv)
            );
            return this;
        }

        /// <summary> 测试事件发生（若存在多个，仅判定首个） </summary>
        public Scenario AssertEvent<T>(Action<T> action = null) where T : EventBase
            => AssertEvent<T>(ev => {
                action?.Invoke(ev);
                return true;
            });

        /// <summary> 测试没有对应的事件发生 </summary>
        public Scenario AssertNoEvent<T>(Predicate<T> predicate = null) where T : EventBase {
            noEventMatchers.Add((ev) => {
                if (ev is T tEv) {
                    return predicate?.Invoke(tEv) ?? true;
                }
                return false;
            });
            return this;
        }

        /// <summary> 测试流局事件发生 </summary>
        public Scenario AssertRyuukyoku<T>(Predicate<T> predicate) where T : RyuukyokuEvent
            => AssertEvent(predicate);

        /// <summary> 测试流局事件发生（若存在多个，仅判定首个） </summary>
        public Scenario AssertRyuukyoku<T>(Action<T> action = null) where T : RyuukyokuEvent
            => AssertEvent(action);

        /// <summary> 测试没有流局事件发生 </summary>
        public Scenario AssertNoRyuukyoku<T>(Predicate<T> predicate = null) where T : RyuukyokuEvent
            => AssertNoEvent(predicate);

        /// <summary> 立即测试现有事件是否匹配 </summary>
        public Scenario ResolveImmediately() {
            int eventI = 0;
            foreach (var matcher in eventMatchers) {
                while (eventI < events.Count && !matcher(events[eventI])) {
                    eventI++;
                }
                if (eventI >= events.Count) {
                    Assert.Fail($"No event matched: {string.Join(", ", events.Select(ev => ev.GetType().Name))}");
                }
            }
            foreach (var matcher in noEventMatchers) {
                if (events.Any((e) => matcher(e))) {
                    Assert.Fail($"Event matched: {string.Join(", ", events.Select(ev => ev.GetType().Name))}");
                }
            }
            eventMatchers.Clear();
            noEventMatchers.Clear();
            events.Clear();
            return this;
        }

        /// <summary> 等待下一次询问操作，并强制匹配现有的事件 </summary>
        public async Task<ScenarioInquiryMatcher> WaitInquiry() {
            var ret = await actionCenter.NextInquiry;
            ResolveImmediately();
            GameSanityCheck(ret);
            return ret;
        }

        /// <summary> 等待游戏结束（无询问操作），并强制匹配现有的事件 </summary>
        public async Task<Scenario> WaitEnd() {
            await runToEnd;
            ResolveImmediately();
            return this;
        }

        /// <summary>
        /// 等待玩家i的回合，并对其中的询问操作全部采用默认选项
        /// 如果下一次询问就是玩家i的回合，不要使用该方法，改为<see cref="WaitInquiry"/>
        /// </summary>
        public async Task<ScenarioInquiryMatcher> WaitPlayerTurn(int playerId) {
            var listener = new EventListener<IncreaseJunEvent>(game.eventBus);
            var tcs = new TaskCompletionSource();
            listener.EarlyAfter((ev) => {
                if (ev.playerId == playerId) {
                    tcs.SetResult();
                    listener.Cancel();
                }
                return Task.CompletedTask;
            });
            for (int i = 0; i < 10; i++) {
                var inquiry = await WaitInquiry();
                if (tcs.Task.IsCompleted) {
                    return inquiry;
                }
                inquiry.Finish();
            }
            throw new Exception($"Player {playerId} did not start their turn in 10 inquiries");
        }

        /// <summary> 测试现有事件并忽略其中的询问操作。（令所有用户选择默认操作） </summary>
        /// <param name="waitCount"> 等待多少询问操作 </param>
        /// <returns>最后一个Inquiry，或null（表示游戏已结束）</returns>
        public async Task<ScenarioInquiryMatcher> Resolve(int waitCount = 1) {
            ScenarioInquiryMatcher inquiry = null;
            for (var i = 0; i < waitCount; i++) {
                try {
                    inquiry?.Finish();
                    inquiry = await actionCenter.NextInquiry;
                } catch (OperationCanceledException) {
                    await runToEnd;
                    inquiry = null;
                    break;
                }
            }
            ResolveImmediately();
            return inquiry;
        }

        /// <summary> 将游戏log以文本形式写入文件 </summary>
        /// <param name="path">文件路径</param>
        /// <returns>自身</returns>
        public Scenario WriteGameLog(string path) {
            // Write json
            var json = new JsonFormatter(new JsonFormatter.Settings(true)).Format(actionCenter.gameLog);
            File.WriteAllText(path + ".json", json);

            // Write binary
            using (var fs = new FileStream(path + ".bin", FileMode.Create)) {
                actionCenter.gameLog.WriteTo(fs);
            }
            return this;
        }
    }

    public class ScenarioBuilder {
        #region GameConfig
        public class GameConfigBuilder {
            private GameConfig config;

            public GameConfigBuilder(int playerCount) {
                var actionCenter = new ScenarioActionCenter(playerCount);
                config = new() {
                    playerCount = playerCount,
                    setup = new ScenarioSetup(actionCenter),
                    actionCenter = actionCenter,
                    seed = 114514,
                };
            }

            /// <summary> 覆盖当前config，但保留setup和actionCenter </summary>
            public GameConfigBuilder OverwriteConfig(GameConfig config) {
                config.setup = this.config.setup;
                config.actionCenter = this.config.actionCenter;
                this.config = config;
                return this;
            }

            /// <summary> 获取Setup实例 </summary>
            public GameConfigBuilder Setup(Action<ScenarioSetup> setup) {
                setup(config.setup as ScenarioSetup);
                return this;
            }

            /// <summary> 设置番缚，默认为1 </summary>
            public GameConfigBuilder SetMinHan(int minHan) {
                config.minHan = minHan;
                return this;
            }

            /// <summary> 设置初始点，默认为25000 </summary>
            public GameConfigBuilder SetInitialPoints(long initialPoints) {
                config.pointThreshold.initialPoints = initialPoints;
                return this;
            }

            /// <summary> 设置立直棒点数，默认为1000 </summary>
            public GameConfigBuilder SetRiichiPoints(long riichiPoints) {
                config.pointThreshold.riichiPoints = riichiPoints;
                return this;
            }

            /// <summary> 设置本场棒总点数，默认为300 </summary>
            public GameConfigBuilder SetHonbaPoints(long honbaPoints) {
                config.pointThreshold.honbaPoints = honbaPoints;
                return this;
            }

            /// <summary> 设置流局总点数，默认为3000 </summary>
            public GameConfigBuilder SetRyuukyokuPoints(params long[] ryuukyokuPoints) {
                config.pointThreshold.ryuukyokuPoints = ryuukyokuPoints;
                return this;
            }

            /// <summary> 设置终局点数，默认为30000 </summary>
            public GameConfigBuilder SetFinishPoints(long finishPoints) {
                config.pointThreshold.finishPoints = finishPoints;
                return this;
            }

            /// <summary> 设置天边，默认为[0, 1000000] </summary>
            public GameConfigBuilder SetPointsRange(params long[] pointsRange) {
                config.pointThreshold.validPointsRange = pointsRange;
                return this;
            }

            /// <summary> 总局数，默认东风战 </summary>
            public GameConfigBuilder SetTotalRound(int totalRound) {
                config.totalRound = totalRound;
                return this;
            }

            /// <summary> 设置随机种子，默认为114514 </summary>
            public GameConfigBuilder SetSeed(ulong seed) {
                config.seed = seed;
                return this;
            }

            /// <summary> 设置食替检测 </summary>
            public GameConfigBuilder SetKuikaePolicy(KuikaePolicy policy) {
                config.kuikaePolicy = policy;
                return this;
            }

            /// <summary> 设置立直要求 </summary>
            public GameConfigBuilder SetRiichiPolicy(RiichiPolicy riichiPolicy) {
                config.riichiPolicy = riichiPolicy;
                return this;
            }

            /// <summary> 设置启用的流局 </summary>
            public GameConfigBuilder SetRyuukyokuTrigger(RyuukyokuTrigger trigger) {
                config.ryuukyokuTrigger = trigger;
                return this;
            }

            /// <summary> 设置连庄策略 </summary>
            public GameConfigBuilder SetRenchanPolicy(RenchanPolicy policy) {
                config.renchanPolicy = policy;
                return this;
            }

            /// <summary> 设置终局策略 </summary>
            public GameConfigBuilder SetEndGamePolicy(EndGamePolicy policy) {
                config.endGamePolicy = policy;
                return this;
            }

            /// <summary> 设置宝牌选项 </summary>
            public GameConfigBuilder SetDoraOption(DoraOption option) {
                config.doraOption = option;
                return this;
            }

            /// <summary> 设置和牌选项 </summary>
            public GameConfigBuilder SetAgariOption(AgariOption option) {
                config.agariOption = option;
                return this;
            }

            /// <summary> 设置计分选项 </summary>
            public GameConfigBuilder SetScoringOption(ScoringOption option) {
                config.scoringOption = option;
                return this;
            }

            public GameConfig Build() {
                return config;
            }
        }

        private readonly GameConfigBuilder configBuilder;

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

            /// <summary> 设置场风，局数，和本场数。默认为东1局0本场。 </summary>
            public GameStateBuilder SetRound(Wind bakaze, int dealer, int honba = 0) {
                this.round = (int)bakaze;
                this.dealer = dealer;
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
                        tiles[fuuroIndex].discardInfo = new DiscardInfo(players[fromPlayer], reason, 0);
                    }
                    return MenLike.From(tiles);
                }
            }
            private long? points;
            private bool? menzen;
            public Tile? riichiTile;
            private bool? wRiichi;
            public Tiles freeTiles = new();
            public Tiles discarded = new();
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
            public PlayerHandBuilder SetPoints(long points) {
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
            /// 指定的舍牌可以少于舍牌数量
            /// </summary>
            /// <param name="count">舍牌数量</param>
            /// <param name="discarded">舍牌</param>
            /// <param name="reservedDiscarded">自动填充时，禁止出现在舍牌里的牌</param>
            public PlayerHandBuilder SetDiscarded(int count, Tiles discarded) {
                if (discarded != null) {
                    this.discarded = discarded;
                }
                this.discardedNum = count;
                return this;
            }

            /// <summary>
            /// 设置舍牌，默认为6张
            /// 若指定的舍牌数量不够则用别的牌填充
            /// </summary>
            /// <param name="count">舍牌数量</param>
            /// <param name="discarded">舍牌，若不设置，则为空</param>
            /// <param name="reservedDiscarded">自动填充时，禁止出现在舍牌里的牌</param>
            public PlayerHandBuilder SetDiscarded(int count, string discarded = null)
                => SetDiscarded(count,
                    discarded == null ? null : new Tiles(discarded));

            /// <summary> 设置手牌 </summary>
            public PlayerHandBuilder SetFreeTiles(Tiles freeTiles) {
                this.freeTiles = freeTiles;
                return this;
            }
            /// <summary> 设置手牌 </summary>
            public PlayerHandBuilder SetFreeTiles(string freeTiles)
                => SetFreeTiles(new Tiles(freeTiles));

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

                // Set data
                this.player = player;
                player.Reset();
                this.handSize = handSize;
                if (points.HasValue) {
                    player.points = points.Value;
                }
                if (menzen.HasValue) {
                    player.hand.menzen = menzen.Value;
                } else {
                    player.hand.menzen = called?.All(x => x.IsClosed) ?? true;
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
            private int revealedDoraCount = 1;
            private int revealedUradoraCount = 1;

            public WallBuilder(IEnumerable<PlayerHandBuilder> players) {
                playerBuilders = players.ToList();
            }

            /// <summary>
            /// 保留一些牌。
            /// 这些牌会被放在牌山最前，下标为0的是第一张牌。
            /// </summary>
            public WallBuilder Reserve(IEnumerable<Tile> tiles) {
                reserved.AddRange(tiles);
                return this;
            }

            /// <summary>
            /// 保留一些牌。
            /// 这些牌会被放在牌山最前，下标为0的是第一张牌。
            /// </summary>
            public WallBuilder Reserve(string tiles) => Reserve(new Tiles(tiles));

            /// <summary>
            /// 保留一张牌。
            /// 这张牌会被放在牌山最前。
            /// </summary>
            public WallBuilder Reserve(Tile tile) => Reserve(Enumerable.Repeat(tile, 1));

            /// <summary> 添加宝牌。第一个宝牌的下标为0。 </summary>
            public WallBuilder AddDoras(IEnumerable<Tile> tiles) {
                doras.AddRange(tiles);
                return this;
            }
            /// <summary> 添加宝牌。第一个宝牌的下标为0。 </summary>
            public WallBuilder AddDoras(string tiles) => AddDoras(new Tiles(tiles));
            /// <summary> 添加宝牌。第一个宝牌的下标为0。 </summary>
            public WallBuilder AddDoras(Tile tile) => AddDoras(Enumerable.Repeat(tile, 1));

            /// <summary> 添加里宝牌。第一个里宝牌的下标为0。 </summary>
            public WallBuilder AddUradoras(IEnumerable<Tile> tiles) {
                uradoras.AddRange(tiles);
                return this;
            }
            /// <summary> 添加里宝牌。第一个里宝牌的下标为0。 </summary>
            public WallBuilder AddUradoras(string tiles) => AddUradoras(new Tiles(tiles));
            /// <summary> 添加里宝牌。第一个里宝牌的下标为0。 </summary>
            public WallBuilder AddUradoras(Tile tile) => AddUradoras(Enumerable.Repeat(tile, 1));

            /// <summary> 添加岭上牌。第一张岭上牌的下标为0。 </summary>
            public WallBuilder AddRinshan(IEnumerable<Tile> tiles) {
                rinshan.AddRange(tiles);
                return this;
            }
            /// <summary> 添加岭上牌。第一张岭上牌的下标为0。 </summary>
            public WallBuilder AddRinshan(string tiles) => AddRinshan(new Tiles(tiles));
            /// <summary> 添加岭上牌。第一张岭上牌的下标为0。 </summary>
            public WallBuilder AddRinshan(Tile tile) => AddRinshan(Enumerable.Repeat(tile, 1));

            /// <summary> 设置有多少Dora已经翻开了，默认为1。 </summary>
            public WallBuilder SetRevealedDoraCount(int count) {
                revealedDoraCount = count;
                return this;
            }

            /// <summary> 设置有多少里Dora已经翻开了，默认为1。 </summary>
            public WallBuilder SetRevealedUradoraCount(int count) {
                revealedUradoraCount = count;
                return this;
            }

            public Wall Setup(Wall wall) {
                if (playerBuilders.Any(builder => builder.player == null)) {
                    throw new InvalidOperationException("Must set up PlayerHandBuilder before setting up wall.");
                }
                wall.Reset();
                var players = playerBuilders.Select(builder => builder.player).ToArray();
                var allTiles = wall.doras.Concat(wall.uradoras).Concat(wall.rinshan).Concat(wall.remaining).ToList();
                wall.revealedDoraCount = revealedDoraCount;
                wall.revealedUradoraCount = revealedUradoraCount;

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
                wall.doras.AddRange(doras.Select(Draw));
                wall.uradoras.AddRange(uradoras.Select(Draw));
                wall.rinshan.AddRange(rinshan.Select(Draw));
                wall.remaining.AddRange(reserved.Select(Draw));

                // Try draw tiles for each player
                foreach (var playerBuilder in playerBuilders) {
                    var hand = playerBuilder.player.hand;

                    // Draw free tiles
                    foreach (var tile in playerBuilder.freeTiles) {
                        hand.Add(Draw(tile));
                    }
                    // Draw called tiles
                    foreach (var tiles in playerBuilder.called) {
                        var gameTiles = tiles.tiles.Select(Draw).ToList();
                        var fuuro = tiles.fuuroIndex >= 0 ? gameTiles[tiles.fuuroIndex] : null;
                        if (tiles.fromPlayer >= 0 && fuuro != null) {
                            var otherHand = playerBuilders[tiles.fromPlayer].player.hand;
                            otherHand.Add(fuuro);
                            otherHand.Play(fuuro, DiscardReason.Draw);
                        }
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
                        var gameTile = Draw(tile);
                        hand.Add(gameTile);
                        hand.Play(gameTile, DiscardReason.Draw);
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
                        hand.Add(DrawNext(reserved));
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
                wall.remaining.AddRange(allTiles);
                wall.remaining.Reverse();
                wall.rinshan.Reverse();
                return wall;
            }
        }
        private readonly WallBuilder wallBuilder;

        public ScenarioBuilder WithWall(Action<WallBuilder> action) {
            action(wallBuilder);
            return this;
        }
        #endregion

        public ScenarioBuilder(int playerCount = 4) {
            configBuilder = new GameConfigBuilder(playerCount);
            playerHandBuilders = new PlayerHandBuilder[playerCount];
            for (var i = 0; i < playerCount; i++) {
                playerHandBuilders[i] = new();
            }
            gameStateBuilder = new GameStateBuilder();
            wallBuilder = new WallBuilder(playerHandBuilders);
        }

        private bool isFirstJun = false;
        /// <summary> 将游戏设为第一巡刚开始的状态 </summary>
        public ScenarioBuilder SetFirstJun() {
            isFirstJun = true;
            foreach (var playerBuilder in playerHandBuilders) {
                if (playerBuilder.discardedNum > 1) {
                    playerBuilder.SetDiscarded(0);
                }
                playerBuilder.SetMenzen(true);
            }
            return this;
        }

        /// <summary> 根据设置的状态创建游戏实例 </summary>
        public Scenario Build(int startPlayerId) {
            var game = new Game(configBuilder.Build());
            gameStateBuilder.Setup(game.info);
            for (int i = 0; i < playerHandBuilders.Length; i++) {
                var player = game.GetPlayer(i);
                playerHandBuilders[i].Setup(player,
                    isFirstJun && player.IsDealer && player.id == startPlayerId ? Game.HAND_SIZE + 1 : Game.HAND_SIZE);
            }
            wallBuilder.Setup(game.wall);
            return new Scenario(game, startPlayerId);
        }

        /// <summary> 根据设置的状态创建游戏实例，并以playerId的回合开始游戏 </summary>
        public Scenario Start(int startPlayerId) {
            return Build(startPlayerId).Start();
        }
    }
}