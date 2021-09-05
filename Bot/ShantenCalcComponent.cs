using HoshinoSharp.Hoshino;
using HoshinoSharp.Hoshino.Message;
using HoshinoSharp.Runtime;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using RabiRiichi.Util;
using SixLabors.ImageSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Bot {
    class ShantenCalcComponent : HoshinoComponent {
        private readonly Hand hand = new Hand();
        private readonly Rand rand = new Rand(Environment.TickCount);
        private readonly BasePattern[] basePatterns = new BasePattern[] {
            new Base33332(),
            new Base72(),
            new Base13_1()
        };

        private int answerShanten = int.MaxValue;
        private Tiles answerTiles = null;
        private bool inGame = false;
        private CancellationTokenSource waitGameEnd = new CancellationTokenSource();

        private async Task Kanbudong(HEvent ev, HBot bot) {
            using var image = Image.Load(Path.Combine(Constants.BASE_DIR, "Resource/Meme/kanbudong.jpg"));
            await bot.Send(ev, HMessage.From(new MessageSegmentImage(image)));
        }

        private async Task StartGame(HEvent ev, HBot bot) {
            var tiles = new Tiles(rand.Choice(Tiles.All, Game.HandSize + 1).Select(tile => tile.WithoutDora));
            tiles.Sort();
            var image = TilesImage.V.Generate(tiles);
            var incoming = new GameTile { tile = tiles[0] };
            tiles.RemoveAt(0);
            hand.hand = new GameTiles(tiles);
            // 开始计算向听数
            answerShanten = int.MaxValue;
            foreach (var pattern in basePatterns) {
                int shanten = pattern.Shanten(hand, incoming, out tiles);
                if (shanten > answerShanten) {
                    continue;
                }
                if (shanten < answerShanten) {
                    answerShanten = shanten;
                    answerTiles = tiles;
                    continue;
                }
                answerTiles.AddRange(tiles);
            }
            answerTiles = new Tiles(answerTiles
                .Select(tile => tile.WithoutDora)
                .Distinct());
            answerTiles.Sort();
            var msg = new HMessage {
                new MessageSegmentImage(image),
                new MessageSegmentText($"向听数几，何切？（30秒）\n例子：向听数1，切25m，则发送\n1 25m\n已经和牌算作向听数-1。")
            };
            await bot.Send(ev, msg);
            image.Dispose();
            inGame = true;

            try {
                await Task.Delay(30 * 1000, waitGameEnd.Token);
            } catch (TaskCanceledException) {
                return;
            }
            inGame = false;
            image = TilesImage.V.Generate(answerTiles);
            await bot.Send(ev, new HMessage {
                new MessageSegmentText($"没有人答对，是{answerShanten}向听，可切这些牌："),
                new MessageSegmentImage(image)
            });
            image.Dispose();
            EndGame();
        }

        private void EndGame() {
            inGame = false;
            answerTiles = null;
            answerShanten = int.MaxValue;
            waitGameEnd.Cancel();
            waitGameEnd.Dispose();
            waitGameEnd = new CancellationTokenSource();
        }

        private async Task CompareAnswer(HEvent ev, HBot bot, string[] answer) {
            if (!int.TryParse(answer[0], out var shanten)) {
                await Kanbudong(ev, bot);
                return;
            }
            if (shanten != answerShanten) {
                return;
            }
            if (shanten >= 0) {
                if (answer.Length <= 1) {
                    await Kanbudong(ev, bot);
                    return;
                }
                Tiles tiles;
                try {
                    tiles = new Tiles(answer[1]);
                } catch (ArgumentException) {
                    await Kanbudong(ev, bot);
                    return;
                }
                tiles.Sort();
                if (answerTiles.ToString() != tiles.ToString()) {
                    return;
                }
            }
            EndGame();
            await bot.Send(ev, "答对了！但并没有什么卯月因为还没有接入数据库。", true);
            return;
        }

        public async Task OnMessage(HEvent ev, HBot bot) {
            var text = ev.message.ExtractPlainText();
            if (text == "有无") {
                bool startGame = false;
                lock (hand) {
                    if (answerTiles == null) {
                        startGame = true;
                        answerTiles = new Tiles();
                    }
                }
                if (startGame) {
                    await StartGame(ev, bot);
                } else {
                    await bot.Send(ev, "同时只能进行一场游戏！");
                }
                return;
            }
            if (!inGame) {
                return;
            }

            var segs = text.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0) {
                return;
            }
            if (segs.Length <= 2) {
                await CompareAnswer(ev, bot, segs);
                return;
            }
        }
    }
}
