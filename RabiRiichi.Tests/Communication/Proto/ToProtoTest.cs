using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Communication.Proto {

    [TestClass]
    public class ToProtoTest {
        [TestMethod]
        public void TestToProtoForAllClasses() {
            var types = typeof(Game).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetMethod("ToProto") != null)
                .ToList();
            var game = new Game(new GameConfig {
                actionCenter = new JsonStringActionCenter(null),
            });
            var tile = new Tile("1s");
            var gameTile = new GameTile(tile, -1);
            game.wall.Reset();
            foreach (var player in game.players) {
                player.Reset();
            }
            game.info.Reset();
            var parameters = new List<object> {
                game,
                game.initialEvent,
                (Kan)new Tiles("1111s").ToMenLike(),
                (Kou)new Tiles("222s").ToMenLike(),
                (Shun)new Tiles("345s").ToMenLike(),
                (Jantou)new Tiles("33s").ToMenLike(),
                (Musou)new Tiles("3s").ToMenLike(),
                true,
                0,
                0L,
                game.config.scoringOption,
                ScoringType.None,
                DiscardReason.None,
                ConcludeGameReason.None,
                ScoreTransferReason.None,
                TileSource.None,
                game.Get<Tanyao>(),
                tile,
                gameTile,
                game.Dealer,
                game.Dealer.hand,
                game.wall,
                Array.Empty<AgariInfo>(),
                Array.Empty<int>(),
                new ScoreTransferList(),
                new List<GameTile>(),
                new List<List<GameTile>>(),
                new List<GameTileMsg>(),
            };
            while (types.Count > 0) {
                bool hasProgress = false;
                foreach (var type in types.ToArray()) {
                    var instance = parameters.FirstOrDefault(p => p.GetType() == type);
                    if (instance == null) {
                        var constructors = type.GetConstructors();
                        var validConstructor = constructors.FirstOrDefault(
                            c => c.GetParameters().All(p => parameters.Any(pa => pa.GetType().IsAssignableTo(p.ParameterType))));
                        if (validConstructor == null) {
                            continue;
                        }
                        instance = validConstructor.Invoke(
                            validConstructor.GetParameters()
                                .Select(p => parameters
                                    .First(pa => pa.GetType().IsAssignableTo(p.ParameterType)))
                                        .ToArray());
                    }

                    hasProgress = true;

                    var method = type.GetMethod("ToProto");
                    var toProtoParams = method.GetParameters();
                    if (toProtoParams.Length == 0) {
                        var proto = method.Invoke(instance, null);
                        Assert.IsNotNull(proto, $"{type.Name} has a ToProto method that returns null");
                    } else {
                        var args = toProtoParams.Select(p =>
                            parameters.FirstOrDefault(pa => pa.GetType().IsAssignableTo(p.ParameterType))).ToArray();
                        var proto = method.Invoke(instance, args);
                        Assert.IsNotNull(proto, $"{type.Name} has a ToProto method that returns null.");
                    }

                    parameters.Add(instance);
                    types.Remove(type);
                }

                if (!hasProgress) {
                    Assert.Fail($"No valid constructor for:\n{string.Join("\n", types.Select(t => t.Name))}");
                    break;
                }
            }
        }
    }
}