﻿using HoshinoSharp.Hoshino;
using HoshinoSharp.Runtime;
using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Bot {
    public class GameComponent : HoshinoComponent {
        public const int NumPlayers = 2;
        public UserList players = new UserList();
        public Game game;
        public HEvent ev;
        public HBot bot;

        public bool IsReady => players.Count >= NumPlayers;

        public void Reset() {
            players.Clear();
            game = new Game(this);
            ev = null;
            bot = null;
        }

        public override void Start() {
            Reset();
        }

        public async Task AddPlayer(HEvent ev, HBot bot) {
            if (IsReady || game.phase != GamePhase.Pending)
                return;
            var player = ev.sender;
            if (players.HasUser(player))
                return;
            players.Add(player);
            if (IsReady) {
                await StartGame(ev, bot);
            } else {
                await bot.Send(ev, $"人数：{players.Count}/{NumPlayers}");
            }
            return;
        }

        public async Task StartGame(HEvent ev, HBot bot) {
            this.bot = bot;
            this.ev = ev;
            game.phase = GamePhase.Running;
            await bot.Send(ev, $"人数：{players.Count}/{NumPlayers}，正在开始游戏……");
            await Task.Delay(1000);
            await game.Start();
            await Task.Delay(1000);
            await bot.Send(ev, $"游戏结束，后面还没写");
            Reset();
        }
    }
}
