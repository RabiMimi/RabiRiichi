using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using RabiRiichi.Server.Agents.Llm;
using System.Linq;

namespace RabiRiichi.Tests.Server.Agents.Llm {
  [TestClass]
  public class LlmEventLogTest {
    [TestMethod]
    public void DiscardLines_KeepRedAndPlainCopiesApart() {
      var log = new LlmEventLog(seat => $"P{seat}", "en");
      var drawn = new GameTile(new Tile("9m"), 9);
      foreach (var tile in new[] { "r3p", "3p", "0p", "r7z", "7z" }) {
        log.Record(new DiscardTileEvent(
            null, 1, new GameTile(new Tile(tile), 1), drawn, DiscardReason.Draw));
      }
      log.Record(new RiichiEvent(
          null, 2, new GameTile(new Tile("r9s"), 2), drawn, DiscardReason.Draw));

      CollectionAssert.AreEqual(
          new[] {
            "P1 discards r3p (from hand)",
            "P1 discards 3p (from hand)",
            "P1 discards 0p (from hand)",
            "P1 discards r7z(Red dragon) (from hand)",
            "P1 discards 7z(Red dragon) (from hand)",
            "P2 declares RIICHI, discards r9s",
          },
          log.Drain(out _).ToList());
    }
  }
}
