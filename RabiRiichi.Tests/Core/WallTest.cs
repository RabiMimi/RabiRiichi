using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Core;
using RabiRiichi.Util;
using System;
using System.Linq;


namespace RabiRiichi.Tests.Core {
    [TestClass]
    public class WallTest {
        private Wall wall;

        [TestInitialize]
        public void Init() {
            wall = new Wall(new RabiRand(114514), new GameConfig());
            wall.Reset();
        }

        [TestMethod]
        public void TestTileNum() {
            int total = Tiles.All.Value.Count;
            wall.RevealDora(false);
            Assert.AreEqual(total, wall.NumRemaining + Wall.NUM_DORA * 2 + Wall.NUM_RINSHAN);
            Assert.AreEqual(Wall.NUM_RINSHAN, wall.rinshan.Count);
            Assert.AreEqual(Wall.NUM_DORA, wall.doras.Count);
            Assert.AreEqual(Wall.NUM_DORA, wall.uradoras.Count);
            Assert.AreEqual(total - 1, wall.hiddenTiles.Count());

            // Num remaining
            int NumRemaining = wall.NumRemaining;
            Assert.IsTrue(wall.Has(NumRemaining));
            Assert.IsFalse(wall.Has(NumRemaining + 1));
            wall.DrawRinshan();
            Assert.AreEqual(NumRemaining - 1, wall.NumRemaining);
            wall.Draw();
            Assert.AreEqual(NumRemaining - 2, wall.NumRemaining);

            // IsHaitei
            Assert.IsFalse(wall.IsHaitei);
            wall.Draw(NumRemaining - 2);
            Assert.IsTrue(wall.IsHaitei);
            Assert.IsTrue(wall.Has(0));
            Assert.IsFalse(wall.Has(1));
        }

        [TestMethod]
        public void TestDraw() {
            int count = wall.NumRemaining;
            var tile = wall.Draw();
            Assert.AreEqual(count - 1, wall.NumRemaining);
            Assert.AreEqual(TileSource.Wall, tile.source);
            CollectionAssert.DoesNotContain(wall.remaining, tile);

            var tiles = wall.Draw(4).ToArray();
            Assert.AreEqual(count - 5, wall.NumRemaining);
            foreach (var t in tiles) {
                CollectionAssert.DoesNotContain(wall.remaining, t);
            }
            Assert.AreEqual(4, tiles.Length);
        }

        [TestMethod]
        public void TestRevealDora() {
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            int hiddenNum = wall.hiddenTiles.Count();
            for (int i = 0; i < Wall.NUM_DORA; i++) {
                var tile = wall.RevealDora(false);
                Assert.IsNotNull(tile);
                Assert.AreEqual(TileSource.Wanpai, tile.source);
            }
            Assert.AreEqual(Wall.NUM_DORA, wall.revealedDoraCount);
            Assert.AreEqual(Wall.NUM_DORA, wall.revealedUradoraCount);
            Assert.AreEqual(hiddenNum - Wall.NUM_DORA, wall.hiddenTiles.Count());
            Assert.IsNull(wall.RevealDora(false));
        }

        [TestMethod]
        public void TestCountDora() {
            // First Dora
            wall.ReplaceDora(0, wall.FindInHidden(new Tile("4s")));
            wall.ReplaceUradora(0, wall.FindInHidden(new Tile("4z")));
            wall.RevealDora(false);
            Assert.AreEqual(0, wall.CountDora(new Tile("6s")));
            Assert.AreEqual(1, wall.CountDora(new Tile("5s")));
            Assert.AreEqual(1, wall.CountDora(new Tile("r5s")));
            Assert.AreEqual(0, wall.CountUradora(new Tile("6z")));
            Assert.AreEqual(1, wall.CountUradora(new Tile("1z")));
            Assert.AreEqual(0, wall.CountUradora(new Tile("5z")));

            // Second Dora
            wall.ReplaceDora(1, wall.FindInHidden(new Tile("4s")));
            wall.ReplaceUradora(1, wall.FindInHidden(new Tile("r7z")));
            wall.RevealDora(false);
            Assert.AreEqual(0, wall.CountDora(new Tile("6s")));
            Assert.AreEqual(2, wall.CountDora(new Tile("5s")));
            Assert.AreEqual(2, wall.CountDora(new Tile("r5s")));
            Assert.AreEqual(0, wall.CountUradora(new Tile("6z")));
            Assert.AreEqual(1, wall.CountUradora(new Tile("1z")));
            Assert.AreEqual(1, wall.CountUradora(new Tile("5z")));

            // Third Dora
            wall.ReplaceDora(2, wall.FindInHidden(new Tile("5s")));
            wall.ReplaceUradora(2, wall.FindInHidden(new Tile("7z")));
            wall.RevealDora(false);
            Assert.AreEqual(1, wall.CountDora(new Tile("6s")));
            Assert.AreEqual(2, wall.CountDora(new Tile("5s")));
            Assert.AreEqual(2, wall.CountDora(new Tile("r5s")));
            Assert.AreEqual(0, wall.CountUradora(new Tile("6z")));
            Assert.AreEqual(1, wall.CountUradora(new Tile("1z")));
            Assert.AreEqual(2, wall.CountUradora(new Tile("5z")));
        }

        [TestMethod]
        public void TestDrawRinshan() {
            int rinshanCount = wall.rinshan.Count;
            var tile = wall.DrawRinshan();
            Assert.AreEqual(TileSource.Wanpai, tile.source);
            Assert.AreEqual(rinshanCount - 1, wall.rinshan.Count);
            CollectionAssert.DoesNotContain(wall.rinshan, tile);
        }

        [TestMethod]
        public void TestRemove() {
            var tile = wall.FindInHidden(new Tile("5s"));
            int remainingCount = wall.remaining.Count;
            Assert.IsTrue(wall.Remove(tile));
            Assert.AreEqual(remainingCount - 1, wall.remaining.Count);
            CollectionAssert.DoesNotContain(wall.remaining, tile);
            Assert.IsFalse(wall.Remove(tile));

            // Remove rinshan
            int rinshanCount = wall.rinshan.Count;
            remainingCount = wall.remaining.Count;
            tile = wall.rinshan[1];
            Assert.IsTrue(wall.Remove(tile));
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(remainingCount - 1, wall.remaining.Count);
            CollectionAssert.DoesNotContain(wall.rinshan, tile);
            Assert.IsFalse(wall.Remove(tile));

            // Remove ura dora
            int uradoraCount = wall.uradoras.Count;
            remainingCount = wall.remaining.Count;
            tile = wall.uradoras[1];
            Assert.IsTrue(wall.Remove(tile));
            Assert.AreEqual(uradoraCount, wall.uradoras.Count);
            Assert.AreEqual(remainingCount - 1, wall.remaining.Count);
            CollectionAssert.DoesNotContain(wall.uradoras, tile);
            Assert.IsFalse(wall.Remove(tile));

            // Remove unrevealed dora
            wall.RevealDora(false);
            int doraCount = wall.doras.Count;
            remainingCount = wall.remaining.Count;
            tile = wall.doras[1];
            Assert.IsTrue(wall.Remove(tile));
            Assert.AreEqual(doraCount, wall.doras.Count);
            Assert.AreEqual(remainingCount - 1, wall.remaining.Count);
            CollectionAssert.DoesNotContain(wall.doras, tile);
            Assert.IsFalse(wall.Remove(tile));

            // Fail to remove revealed dora
            tile = wall.doras[0];
            Assert.IsFalse(wall.Remove(tile));

            // Fail to remove dora when haitei
            tile = wall.uradoras[2];
            wall.Draw(wall.NumRemaining);
            Assert.IsFalse(wall.Remove(tile));
        }

        [TestMethod]
        public void TestInsertRemaining() {
            // Insert tile in remaining
            int remainingCount = wall.remaining.Count;
            var tile = wall.remaining[3];
            wall.Insert(0, tile);
            Assert.AreEqual(remainingCount, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[^1]);

            wall.Insert(wall.remaining.Count - 1, tile);
            Assert.AreEqual(remainingCount, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[0]);

            wall.InsertFirst(tile);
            Assert.AreEqual(remainingCount, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[^1]);

            wall.InsertLast(tile);
            Assert.AreEqual(remainingCount, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[0]);

            // Insert tile not in remaining
            tile = wall.Draw();
            remainingCount = wall.remaining.Count;
            wall.Insert(0, tile);
            Assert.AreEqual(remainingCount + 1, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[^1]);
            wall.Remove(tile);

            wall.Insert(wall.remaining.Count, tile);
            Assert.AreEqual(remainingCount + 1, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[0]);
            wall.Remove(tile);

            wall.InsertFirst(tile);
            Assert.AreEqual(remainingCount + 1, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[^1]);
            wall.Remove(tile);

            wall.InsertLast(tile);
            Assert.AreEqual(remainingCount + 1, wall.remaining.Count);
            Assert.AreEqual(tile, wall.remaining[0]);
            wall.Remove(tile);
        }

        [TestMethod]
        public void TestReplaceRemaining() {
            var tile = wall.Draw();
            int remainingCount = wall.remaining.Count;
            var toReplace = wall.remaining[^4];
            Assert.AreEqual(toReplace, wall.Replace(3, tile));
            Assert.AreEqual(remainingCount, wall.remaining.Count);

            toReplace = wall.remaining[^1];
            Assert.AreEqual(toReplace, wall.ReplaceFirst(tile));

            toReplace = wall.remaining[0];
            Assert.AreEqual(toReplace, wall.ReplaceLast(tile));
        }

        [TestMethod]
        public void TestPlaceDora() {
            // Place tile in dora
            int doraCount = wall.doras.Count;
            var tile = wall.doras[1];
            var toPlace = wall.doras[0];
            wall.PlaceDora(0, tile);
            Assert.AreEqual(doraCount, wall.doras.Count);
            Assert.AreEqual(tile, wall.doras[0]);
            Assert.AreEqual(toPlace, wall.doras[1]);

            // Place tile not in dora
            tile = wall.rinshan[1];
            toPlace = wall.doras[2];
            wall.PlaceDora(2, tile);
            Assert.AreEqual(doraCount, wall.doras.Count);
            Assert.AreEqual(tile, wall.doras[2]);
            Assert.AreEqual(toPlace, wall.rinshan[1]);

            // Place itself
            tile = wall.doras[0];
            wall.PlaceDora(0, tile);
            Assert.AreEqual(doraCount, wall.doras.Count);
            Assert.AreEqual(tile, wall.doras[0]);

            // Place nonexistent tile
            tile = wall.Draw();
            Assert.ThrowsException<ArgumentException>(() => wall.PlaceDora(0, tile));
        }

        [TestMethod]
        public void TestPlaceUraDora() {
            // Place tile in uradora
            int uradoraCount = wall.uradoras.Count;
            var tile = wall.uradoras[1];
            var toPlace = wall.uradoras[0];
            wall.PlaceUradora(0, tile);
            Assert.AreEqual(uradoraCount, wall.uradoras.Count);
            Assert.AreEqual(tile, wall.uradoras[0]);
            Assert.AreEqual(toPlace, wall.uradoras[1]);

            // Place tile not in uradora
            tile = wall.doras[1];
            toPlace = wall.uradoras[2];
            wall.PlaceUradora(2, tile);
            Assert.AreEqual(uradoraCount, wall.uradoras.Count);
            Assert.AreEqual(tile, wall.uradoras[2]);
            Assert.AreEqual(toPlace, wall.doras[1]);

            // Place itself
            tile = wall.uradoras[0];
            wall.PlaceUradora(0, tile);
            Assert.AreEqual(uradoraCount, wall.uradoras.Count);
            Assert.AreEqual(tile, wall.uradoras[0]);

            // Place nonexistent tile
            tile = wall.Draw();
            Assert.ThrowsException<ArgumentException>(() => wall.PlaceUradora(0, tile));
        }

        [TestMethod]
        public void TestPlaceRinshan() {
            // Place tile in rinshan
            int rinshanCount = wall.rinshan.Count;
            var tile = wall.rinshan[1];
            var toPlace = wall.rinshan[^1];
            wall.PlaceRinshan(0, tile);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.rinshan[^1]);
            Assert.AreEqual(toPlace, wall.rinshan[1]);

            // Place tile not in rinshan
            tile = wall.remaining[1];
            toPlace = wall.rinshan[^2];
            wall.PlaceRinshan(1, tile);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.rinshan[^2]);
            Assert.AreEqual(toPlace, wall.remaining[1]);

            // Place itself
            tile = wall.rinshan[^2];
            wall.PlaceRinshan(1, tile);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.rinshan[^2]);

            // Place nonexistent tile
            tile = wall.Draw();
            Assert.ThrowsException<ArgumentException>(() => wall.PlaceRinshan(0, tile));

            // Place rinshan first
            tile = wall.rinshan[0];
            toPlace = wall.rinshan[^1];
            wall.PlaceRinshanFirst(tile);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.rinshan[^1]);
            Assert.AreEqual(toPlace, wall.rinshan[0]);
            Assert.AreEqual(tile, wall.DrawRinshan());

            // Place rinshan last
            rinshanCount = wall.rinshan.Count;
            tile = wall.rinshan[^1];
            toPlace = wall.rinshan[0];
            wall.PlaceRinshanLast(tile);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.rinshan[0]);
            Assert.AreEqual(toPlace, wall.rinshan[^1]);
        }

        [TestMethod]
        public void TestReplaceDora() {
            var tile = wall.Draw();
            var toReplace = wall.doras[3];
            int doraCount = wall.doras.Count;
            Assert.AreEqual(toReplace, wall.ReplaceDora(3, tile));
            Assert.AreEqual(tile, wall.doras[3]);
            Assert.AreEqual(doraCount, wall.doras.Count);
        }

        [TestMethod]
        public void TestReplaceUradora() {
            var tile = wall.Draw();
            var toReplace = wall.uradoras[3];
            int uradoraCount = wall.uradoras.Count;
            Assert.AreEqual(toReplace, wall.ReplaceUradora(3, tile));
            Assert.AreEqual(tile, wall.uradoras[3]);
            Assert.AreEqual(uradoraCount, wall.uradoras.Count);
        }

        [TestMethod]
        public void TestReplaceRinshan() {
            var tile = wall.Draw();
            var toReplace = wall.rinshan[^3];
            int rinshanCount = wall.rinshan.Count;
            Assert.AreEqual(toReplace, wall.ReplaceRinshan(2, tile));
            Assert.AreEqual(tile, wall.rinshan[^3]);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);

            // Replace rinshan first
            tile = wall.rinshan[0];
            toReplace = wall.rinshan[^1];
            Assert.AreEqual(toReplace, wall.ReplaceRinshanFirst(tile));
            Assert.AreEqual(tile, wall.rinshan[^1]);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
            Assert.AreEqual(tile, wall.DrawRinshan());

            // Replace rinshan last
            rinshanCount = wall.rinshan.Count;
            tile = wall.rinshan[^1];
            toReplace = wall.rinshan[0];
            Assert.AreEqual(toReplace, wall.ReplaceRinshanLast(tile));
            Assert.AreEqual(tile, wall.rinshan[0]);
            Assert.AreEqual(rinshanCount, wall.rinshan.Count);
        }

        #region Dora Options Test
        [TestMethod]
        public void TestDoraOption_EverythingOn() {
            wall.config.doraOption = DoraOption.All;
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(false));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(true));
            Assert.AreEqual(2, wall.revealedDoraCount);
            Assert.AreEqual(2, wall.revealedUradoraCount);
        }

        [TestMethod]
        public void TestDoraOption_InitialDoraOff() {
            wall.config.doraOption = DoraOption.All & ~DoraOption.InitialDora;
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNull(wall.RevealDora(false));
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(true));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(2, wall.revealedUradoraCount);
        }

        [TestMethod]
        public void TestDoraOption_InitialUradoraOff() {
            wall.config.doraOption = DoraOption.All & ~DoraOption.InitialUradora;
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(false));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(true));
            Assert.AreEqual(2, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
        }

        [TestMethod]
        public void TestDoraOption_KanDoraOff() {
            wall.config.doraOption = DoraOption.All & ~DoraOption.KanDora;
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(false));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
            Assert.IsNull(wall.RevealDora(true));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(2, wall.revealedUradoraCount);
        }

        [TestMethod]
        public void TestDoraOption_KanUradoraOff() {
            wall.config.doraOption = DoraOption.All & ~DoraOption.KanUradora;
            Assert.AreEqual(0, wall.revealedDoraCount);
            Assert.AreEqual(0, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(false));
            Assert.AreEqual(1, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
            Assert.IsNotNull(wall.RevealDora(true));
            Assert.AreEqual(2, wall.revealedDoraCount);
            Assert.AreEqual(1, wall.revealedUradoraCount);
        }
        #endregion
    }
}