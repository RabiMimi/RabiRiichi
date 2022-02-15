using RabiRiichi.Event;
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
        public PatternResolver patternResolver;
        public Rand rand;

        public Game(GameConfig config) {
            rand = new Rand((int)(DateTimeOffset.Now.ToUnixTimeSeconds() & 0xffffffff));
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(this);
            serviceCollection.AddSingleton(rand);
            serviceCollection.AddSingleton(gameInfo);
            serviceCollection.AddSingleton(config);
            serviceCollection.AddSingleton<Wall>();
            serviceCollection.AddSingleton<EventBus>();
            serviceCollection.AddSingleton<GameInfo>();
            serviceCollection.AddSingleton<ActionManager>();
            serviceCollection.AddSingleton<PatternResolver>();
            diContainer = serviceCollection.BuildServiceProvider();
            eventBus = diContainer.GetService<EventBus>();
            gameInfo = diContainer.GetService<GameInfo>();
            actionManager = diContainer.GetService<ActionManager>();
            wall = diContainer.GetService<Wall>();
            patternResolver = diContainer.GetService<PatternResolver>();
        }

        #region GameUtil
        public bool IsYaku(Tile tile) {
            return tile.IsSangen || tile.IsSame(Tile.From(gameInfo.wind));
        }
        public Player GetPlayer(int index) => players[index];
        public int Time => gameInfo.timeStamp;
        #endregion

        #region Start

        public async Task Start() {
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
    }
}
