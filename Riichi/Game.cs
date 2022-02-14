using RabiRiichi.Event;
using RabiRiichi.Event.Listener;
using RabiRiichi.Pattern;
using RabiRiichi.Resolver;
using RabiRiichi.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Riichi {
    public enum GamePhase {
        Pending, Running, Finished
    }

    public class GameInfo {
        public GamePhase phase = GamePhase.Pending;
        public int playerCount = 2;
        public Wind wind = Wind.E;
        public int round = 0;
        public int honba = 0;
    }

    public class Game {
        public const int HandSize = 13;

        public GameInfo gameInfo;
        public Player[] players;

        public EventBus eventBus;
        public Wall wall;
        public ActionManager actionManager;
        public Rand rand;

        public Game(GameInfo info) {
            gameInfo = info;
        }

        public Player GetPlayer(int index) => players[index];

        #region Start
        protected virtual void RegisterEventListeners() {
            eventBus.Register<DealHandEvent>(Phase.On, new DftOnDealHand());
            eventBus.Register<DealHandEvent>(Phase.Post, new DftPostDealHand());

            eventBus.Register<DrawTileEvent>(Phase.On, new DftOnDrawTile());
            eventBus.Register<DrawTileEvent>(Phase.Post, new DftPostDrawTile());

            eventBus.Register<GetTileEvent>(Phase.Pre, new DftTriggerUpdate());
            eventBus.Register<GetTileEvent>(Phase.Post, new DftPostGetTile());

            eventBus.Register<PlayTileEvent>(Phase.Post, new DftPostPlayTile());
            eventBus.Register<PlayTileEvent>(Phase.Finalize, new DftFinPlayTile());

            eventBus.Register<EventBase>(Phase.Finalize, new MessageSender());
        }

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

        protected virtual void RegisterPatterns() {
            var basePatterns = new BasePattern[] {
                new Base33332(),
                new Base72(),
                new Base13_1()
            };
            var stdPatterns = new StdPattern[] {
                new Pinfu(),
            };
            var bonusPatterns = new StdPattern[] {
                new Akadora(),
            };
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

        protected virtual void Init() {
            players = null;
            eventBus = new EventBus { game = this };
            wall = new Wall();
            actionManager = new ActionManager();
            rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
            roundTime = 0;
        }

        public async Task Start() {
            // Initialize rand
            Init();

            // Register
            RegisterEventListeners();
            RegisterResolvers();
            RegisterPatterns();

            // Init players
            players = new Player[gameInfo.playerCount];
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player(i, this) {
                    wind = (Wind)i,
                };
            }

            // Deal cards
            for (int i = 0; i < players.Length; i++) {
                var ev = new DealHandEvent {
                    game = this,
                    player = i,
                };
                eventBus.Queue(ev);
            }
            eventBus.Queue(new DrawTileEvent {
                game = this,
                type = DrawTileType.Wall,
                player = GetPlayer(0)
            });

            // 游戏逻辑
            while (!eventBus.Empty) {
                await eventBus.Process();
            }

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

        public void DrawNext(int player) {
            eventBus.Queue(new DrawTileEvent {
                game = this,
                type = DrawTileType.Wall,
                player = NextPlayer(player),
            });
        }
        #endregion
    }
}
