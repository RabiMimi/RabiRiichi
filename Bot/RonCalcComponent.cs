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
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Bot {
    class RonCalcComponent : HoshinoComponent {
        private bool HIDE_TILE = false;
        private readonly Hand hand = new Hand();
        private readonly Rand rand = new Rand(Environment.TickCount);
        private readonly BasePattern[] basePatterns = new BasePattern[] {
            new Base33332(),
            new Base72(),
            new Base13_1()
        };

        private Tiles answerTiles = null;
        private GameTile hiddenTile;
        private bool inGame = false;
        private CancellationTokenSource waitGameEnd = new CancellationTokenSource();

        private async Task Kanbudong(HEvent ev, HBot bot) {
            using var image = Image.Load(Path.Combine(Constants.BASE_DIR, "Resource/Meme/kanbudong.jpg"));
            await bot.Send(ev, HMessage.From(new MessageSegmentImage(image)));
        }

        private int GetShanten(Hand hand, GameTile incoming, out Tiles outTiles) {
            int minShanten = int.MaxValue;
            outTiles = new Tiles();
            //HUtil.Log($"Hand = {hand.hand}, incoming = {incoming}");
            foreach (var pattern in basePatterns) {
                int shanten = pattern.Shanten(hand, incoming, out var tiles);
                //HUtil.Log($"[{pattern.GetType().Name}] Shaten = {shanten}, tiles = {tiles}");
                if (shanten > minShanten) {
                    continue;
                }
                if (shanten < minShanten) {
                    minShanten = shanten;
                    outTiles = tiles;
                    continue;
                }
                outTiles.AddRange(tiles);
            }
            outTiles = new Tiles(outTiles.Distinct());
            outTiles.Sort();
            return minShanten;
        }

        private bool InitGame() {
            var wall = new Wall();
            var group = HIDE_TILE ? Group.Invalid : (Group)rand.Next(0, 5);
            wall.remaining = new Tiles(wall.remaining
                .Where(tile => group == Group.Invalid ? true : tile.Gr == group)
                .Select(tile => tile.WithoutDora));
            var tiles = new Tiles(rand.Choice(wall.remaining, Game.HandSize));
            wall.Draw(tiles);
            tiles.Sort();
            hand.hand = new GameTiles(tiles);
            // 开始强制摸到听牌
            int currentShanten;
            while ((currentShanten = GetShanten(hand, null, out answerTiles)) > 0) {
                if (currentShanten > 2) {
                    return false;
                }
                answerTiles.RemoveAll(tile => !wall.remaining.Contains(tile));
                if (answerTiles.Count == 0) {
                    return false;
                }
                var tile = rand.Choice(answerTiles);
                var gameTile = new GameTile { tile = tile };
                wall.Draw(tile);
                //HUtil.Log("Draw: " + tile);
                GetShanten(hand, gameTile, out var discardTiles);
                tile = rand.Choice(discardTiles);
                //HUtil.Log("Discard: " + tile);
                hand.Add(gameTile);
                hand.hand.RemoveAt(hand.hand.FindIndex(gTile => gTile.tile.IsSame(tile)));
                hand.hand.Sort();
            }
            hiddenTile = HIDE_TILE ? rand.Choice(hand.hand) : null;
            return true;
        }

        private async Task StartGame(HEvent ev, HBot bot) {
            HIDE_TILE = rand.Next(0, 2) == 0;
            await bot.Send(ev, "正在洗牌……");
            while (!InitGame()) {
                await Task.Yield();
            }
            var displayTiles = hand.hand
                .Where(tile => tile != hiddenTile)
                .Select(tile => tile.tile);
            if (hiddenTile != null) {
                displayTiles = displayTiles.Append(Tile.Empty);
            }
            var image = TilesImage.V.Generate(new Tiles(displayTiles));
            var msg = new HMessage {
                new MessageSegmentImage(image),
                new MessageSegmentText(hiddenTile != null
                    ? "有一张牌看不清。猜猜听哪些牌？（30秒）"
                    : "听哪些牌？（30秒）")
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
            Image hiddenImage = hiddenTile == null ? null
                : TilesImage.V.Generate(new Tiles { hiddenTile.tile });
            msg.Clear();
            if (hiddenImage != null) {
                msg.Add(new MessageSegmentText("没有人答对，藏起来的牌是"));
                msg.Add(new MessageSegmentImage(hiddenImage));
                msg.Add(new MessageSegmentText("听这些牌："));
            } else {
                msg.Add(new MessageSegmentText("没有人答对，听这些牌："));
            }
            msg.Add(new MessageSegmentImage(image));
            await bot.Send(ev, msg);
            hiddenImage?.Dispose();
            image.Dispose();
            EndGame();
        }

        private void EndGame() {
            inGame = false;
            answerTiles = null;
            hiddenTile = null;
            waitGameEnd.Cancel();
            waitGameEnd.Dispose();
            waitGameEnd = new CancellationTokenSource();
        }

        private async Task CompareAnswer(HEvent ev, HBot bot, string answer) {
            answer = answer.Trim();
            if (!answer.All(c => char.IsLetterOrDigit(c))) {
                return;
            }
            Tiles tiles;
            try {
                tiles = new Tiles(answer);
            } catch (ArgumentException) {
                await Kanbudong(ev, bot);
                return;
            }
            tiles.Sort();
            if (answerTiles.ToString() != tiles.ToString()) {
                return;
            }
            Image hiddenImage = hiddenTile == null ? null
                : TilesImage.V.Generate(new Tiles { hiddenTile.tile });
            EndGame();

            var msg = new HMessage();
            if (hiddenImage != null) {
                msg.Add(new MessageSegmentText("答对了！藏起来的牌是"));
                msg.Add(new MessageSegmentImage(hiddenImage));
                msg.Add(new MessageSegmentText("但并没有什么卯月因为还没有接入数据库。"));
            } else {
                msg.Add(new MessageSegmentText("答对了！但并没有什么卯月因为还没有接入数据库。"));
            }
            await bot.Send(ev, msg, true);
            hiddenImage?.Dispose();
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

            await CompareAnswer(ev, bot, text);
        }
    }
}
