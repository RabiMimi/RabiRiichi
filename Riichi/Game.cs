using HoshinoSharp.Hoshino;
using RabiRiichi.Bot;
using RabiRiichi.Event;
using RabiRiichi.Event.Listener;
using RabiRiichi.Pattern;
using RabiRiichi.Resolver;
using RabiRiichi.Util;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Riichi {
    public enum GamePhase {
        Pending, Running, Finished
    }
    public class Game {
        public const int HandSize = 13;

        public GamePhase phase = GamePhase.Pending;
        public GameComponent hoshino;
        public Player[] players;

        public EventBus eventBus;
        public Wall wall;
        public ActionManager actionManager;
        public Rand rand;

        public Game(GameComponent component) {
            hoshino = component;
        }

        public UserInfo GetUser(int index) => hoshino.players[index];
        public Player GetPlayer(int index) => players[index];

        #region Start
        protected virtual void RegisterEventListeners() {
            eventBus.Register<DealHandEvent>(Phase.On, new DefaultOnDealHand());
            eventBus.Register<DealHandEvent>(Phase.Finalize, new DefaultFinalizeDealHand());
            eventBus.Register<EventBase>(Phase.Finalize, new MessageSender());
        }

        protected virtual void RegisterResolvers() {
            actionManager.RegisterResolver(new RonResolver());
            actionManager.RegisterResolver(new RiichiResolver());
            actionManager.RegisterResolver(new ChiResolver());
            actionManager.RegisterResolver(new KanResolver());
            actionManager.RegisterResolver(new PonResolver());
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
            eventBus = new EventBus();
            wall = new Wall();
            actionManager = new ActionManager();
            rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
        }

        public async Task Start() {
            // Initialize rand
            Init();

            // Register
            RegisterEventListeners();
            RegisterResolvers();
            RegisterPatterns();

            // Init players
            players = new Player[hoshino.players.Count];
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player {
                    id = i,
                    wind = (Wind)i
                };
            }

            // Deal cards
            for (int i = 0; i < players.Length; i++) {
                var ev = new DealHandEvent {
                    game = this,
                    player = i,
                };
                await eventBus.Process(ev);
            }

            // End game
            phase = GamePhase.Finished;
        }
        #endregion

        #region Player
        public int NextPlayer(int id) {
            return id == players.Length - 1 ? 0 : id + 1;
        }
        public int PrevPlayer(int id) {
            return id == 0 ? players.Length - 1 : id - 1;
        }
        #endregion
    }
}
