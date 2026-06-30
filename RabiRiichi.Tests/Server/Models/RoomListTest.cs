using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Models;
using System;
using System.Linq;

namespace RabiRiichi.Tests.Server.Models {
  [TestClass]
  public class RoomListTest {
    [TestMethod]
    public void TestAddAndGetAndRemove() {
      var rand = new Random(0);
      var roomList = new RoomList(rand);
      
      var config = new GameConfig();
      var room1 = new Room(rand, config);
      var room2 = new Room(rand, config);

      Assert.IsTrue(roomList.Add(room1));
      Assert.IsTrue(roomList.Add(room2));

      // IDs should be in range 1000-10000
      Assert.IsTrue(room1.id >= 1000 && room1.id < 10000);
      Assert.IsTrue(room2.id >= 1000 && room2.id < 10000);
      Assert.AreNotEqual(room1.id, room2.id);

      // Get
      var fetched1 = roomList.Get(room1.id);
      Assert.AreEqual(room1, fetched1);

      // TryGet
      Assert.IsTrue(roomList.TryGet(room2.id, out var fetched2));
      Assert.AreEqual(room2, fetched2);

      // Enumerable
      var list = roomList.ToList();
      Assert.AreEqual(2, list.Count);
      Assert.IsTrue(list.Contains(room1));
      Assert.IsTrue(list.Contains(room2));

      // Remove
      Assert.IsTrue(roomList.Remove(room1.id));
      Assert.IsNull(roomList.Get(room1.id));

      // TryRemove
      Assert.IsTrue(roomList.TryRemove(room2.id, out var removed2));
      Assert.AreEqual(room2, removed2);
      Assert.IsFalse(roomList.TryGet(room2.id, out _));

      Assert.AreEqual(0, roomList.Count());
    }
  }
}
