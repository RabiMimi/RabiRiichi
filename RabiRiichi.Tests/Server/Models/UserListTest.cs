using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Models;
using System.Linq;

namespace RabiRiichi.Tests.Server.Models {
  [TestClass]
  public class UserListTest {
    [TestMethod]
    public void TestAddAndGetAndRemove() {
      var userList = new UserList();
      var user1 = new User { nickname = "Alice" };
      var user2 = new User { nickname = "Bob" };

      Assert.IsTrue(userList.Add(user1));
      Assert.IsTrue(userList.Add(user2));

      // IDs should be auto-incremented and non-zero
      Assert.AreNotEqual(0, user1.id);
      Assert.AreNotEqual(0, user2.id);
      Assert.AreNotEqual(user1.id, user2.id);

      // Get
      var fetched1 = userList.Get(user1.id);
      Assert.AreEqual(user1, fetched1);
      Assert.AreEqual("Alice", fetched1.nickname);

      // TryGet
      Assert.IsTrue(userList.TryGet(user2.id, out var fetched2));
      Assert.AreEqual(user2, fetched2);

      // Enumerable
      var list = userList.ToList();
      Assert.AreEqual(2, list.Count);
      Assert.IsTrue(list.Contains(user1));
      Assert.IsTrue(list.Contains(user2));

      // TryRemove
      Assert.IsTrue(userList.TryRemove(user1.id, out var removed1));
      Assert.AreEqual(user1, removed1);
      Assert.IsNull(userList.Get(user1.id));
      Assert.IsFalse(userList.TryGet(user1.id, out _));

      Assert.AreEqual(1, userList.Count());
    }
  }
}
