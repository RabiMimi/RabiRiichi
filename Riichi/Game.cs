﻿using RabiRiichi.Event;
using RabiRiichi.Event.Listener;
using RabiRiichi.Pattern;
using RabiRiichi.Resolver;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RabiRiichi.Riichi {

    public class Game {
        public const int HandSize = 13;
        public ServiceProvider diContainer;

        public GameInfo gameInfo;
        public Player[] players;

        public EventBus eventBus;
        public Wall wall;
        public ActionManager actionManager;
        public Rand rand;

        public Game(GameConfig config) {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(this);
            serviceCollection.AddSingleton(config);
            serviceCollection.AddSingleton<Wall>();
            serviceCollection.AddSingleton<EventBus>();
            serviceCollection.AddSingleton<GameInfo>();
            serviceCollection.AddSingleton<ActionManager>();
            diContainer = serviceCollection.BuildServiceProvider();
            eventBus = diContainer.GetService<EventBus>();
            gameInfo = diContainer.GetService<GameInfo>();
            actionManager = diContainer.GetService<ActionManager>();
            wall = diContainer.GetService<Wall>();
        }

        #region GameUtil
        public bool IsYaku(Tile tile) {
            return tile.IsSangen || tile.IsSame(Tile.From(gameInfo.wind));
        }
        public Player GetPlayer(int index) => players[index];
        #endregion

        #region Start

        protected virtual void RegisterResolvers() {
            var resolvers = new ResolverBase[] {
                new RonResolver(),
                new RiichiResolver(),
                new ChiResolver(),
                new KanResolver(),
                new PonResolver(),
                new PlayTileResolver(),
            };
            foreach (var resolver in resolvers) {
                actionManager.RegisterResolver(resolver);
            }
        }

/*
        protected virtual void RegisterPatterns() {
            if (actionManager.TryGetResolver<RonResolver>(out var ronResolver)) {
                ronResolver.SetMinHan(1);
                foreach (var pattern in basePatterns) {
                    ronResolver.RegisterBasePattern(pattern);
                }
                foreach (var pattern in stdPatterns) {
                    ronResolver.RegisterStdPattern(pattern);
                }
                foreach (var pattern in bonusPatterns) {
                    ronResolver.RegisterBonusPattern(pattern);
                }
            }
            if (actionManager.TryGetResolver<RiichiResolver>(out var riichiResolver)) {
                foreach (var pattern in basePatterns) {
                    riichiResolver.RegisterBasePattern(pattern);
                }
            }
        }
*/

        protected virtual void Init() {
            wall = new Wall();
            actionManager = new ActionManager();
            rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
            roundTime = 0;
        }

        public async Task Start() {
            // Initialize rand
            Init();

            // Register
            RegisterResolvers();
            // RegisterPatterns();

            // Init players
            players = new Player[gameInfo.config.playerCount];
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player(i, this) {
                    wind = (Wind)i,
                };
            }

            // Deal cards
            foreach (var player in players) {
                var ev = new DealHandEvent(this, player);
                eventBus.Queue(ev);
            }
            eventBus.Queue(new DrawTileEvent(this, GetPlayer(this.gameInfo.Banker), DrawTileType.Wall));

            // 游戏逻辑
            await eventBus.ProcessQueue();

            // End game
            gameInfo.phase = GamePhase.Finished;
        }
        #endregion

        #region Player
        public int NextPlayerId(int id) => id == players.Length - 1 ? 0 : id + 1;

        public int PrevPlayerId(int id) => id == 0 ? players.Length - 1 : id - 1;

        public Player NextPlayer(int id) => players[NextPlayerId(id)];

        public Player PrevPlayer(int id) => players[PrevPlayerId(id)];

        public IEnumerable<GameTile> AllDiscardedTiles =>
            players.SelectMany(player => player.hand.discarded);
        #endregion

        #region Timer
        private int roundTime;

        /// <summary>
        /// （进入下一个时间点并）返回当前时间
        /// </summary>
        /// <param name="advance">是否进入下一个时间点</param>
        /// <returns></returns>
        public int Time(bool advance = true) {
            return advance ? ++roundTime : roundTime;
        }
        #endregion

        #region Update
        public void OnUpdate() {
            // 更新振听状态
            var resolver = actionManager.GetResolver<RiichiResolver>();
            foreach (var player in players) {
                player.hand.furiten = resolver.IsFuriten(player.hand);
            }
        }
        #endregion
    }
}
