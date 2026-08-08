using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Arena.Storage;
using System;
using System.IO;
using System.Linq;

namespace RabiRiichi.Tests.Server.Arena {
  [TestClass]
  public class RatingStoreTest {
    private string workspaceDir;

    [TestInitialize]
    public void Setup() {
      workspaceDir = Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory, $"arena_rating_{Guid.NewGuid():N}");
      Directory.CreateDirectory(workspaceDir);
    }

    [TestCleanup]
    public void Cleanup() {
      if (Directory.Exists(workspaceDir)) {
        try {
          Directory.Delete(workspaceDir, recursive: true);
        } catch {
          // ignore
        }
      }
    }

    [TestMethod]
    public void TestGetMissingReturnsNull() {
      var store = new RatingStore(workspaceDir);
      Assert.IsNull(store.Get("gpt-x"));
      Assert.AreEqual(0, store.GetAll().Count);
    }

    [TestMethod]
    public void TestUpdateAndGet() {
      var store = new RatingStore(workspaceDir);
      store.Update(new RatingRecord {
        ModelId = "gpt-x", Elo = 1520, Games = 4, Wins = 2,
        Place1 = 2, Place2 = 1, Place3 = 1, Place4 = 0, Penalties = 1,
      });

      var got = store.Get("gpt-x");
      Assert.IsNotNull(got);
      Assert.AreEqual(1520, got.Elo);
      Assert.AreEqual(4, got.Games);
      Assert.AreEqual(2, got.Place1);
      Assert.AreEqual(1, got.Penalties);

      // Mutating the returned copy must not affect the store.
      got.Elo = 9999;
      Assert.AreEqual(1520, store.Get("gpt-x").Elo);
    }

    [TestMethod]
    public void TestRoundTripAcrossReload() {
      var store = new RatingStore(workspaceDir);
      store.Update(new RatingRecord { ModelId = "gpt-x", Elo = 1520, Games = 3 });
      store.Update(new RatingRecord { ModelId = "baseline-mid", Elo = 1500, Games = 3 });

      var reloaded = new RatingStore(workspaceDir);
      Assert.AreEqual(2, reloaded.GetAll().Count);
      Assert.AreEqual(1520, reloaded.Get("gpt-x").Elo);
      Assert.AreEqual(1500, reloaded.Get("baseline-mid").Elo);
    }

    [TestMethod]
    public void TestUpdateReplacesExisting() {
      var store = new RatingStore(workspaceDir);
      store.Update(new RatingRecord { ModelId = "gpt-x", Elo = 1500, Games = 1 });
      store.Update(new RatingRecord { ModelId = "gpt-x", Elo = 1533, Games = 2 });

      Assert.AreEqual(1, store.GetAll().Count);
      Assert.AreEqual(1533, store.Get("gpt-x").Elo);
      Assert.AreEqual(2, store.Get("gpt-x").Games);
    }
  }
}
