using HoshinoSharp.Hoshino;
using RabiRiichi.Bot;
using RabiRiichi.Event;
using RabiRiichi.Event.Listener;
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
        public EventBus eventBus = new EventBus();
        public Yama yama = new Yama();
        public Rand rand;

        public Game(GameComponent component) {
            hoshino = component;
        }

        public UserInfo GetUser(int index) => hoshino.players[index];
        public Player GetPlayer(int index) => players[index];

        #region Start
        public async Task Start() {
            // Register events
            eventBus.Register<DealHandEvent>(Phase.On, new DefaultOnDealHand());
            eventBus.Register<DealHandEvent>(Phase.Finalize, new DefaultFinalizeDealHand());
            eventBus.Register<EventBase>(Phase.Finalize, new MessageSender());

            // Init players
            players = new Player[hoshino.players.Count];
            for (int i = 0; i < players.Length; i++) {
                players[i] = new Player {
                    id = i,
                    wind = (Wind)i
                };
            }

            // Deal cards
            rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
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
    }
}
