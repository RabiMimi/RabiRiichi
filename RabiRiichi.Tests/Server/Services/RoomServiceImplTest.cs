using Microsoft.VisualStudio.TestTools.UnitTesting;
using Grpc.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Server.Models;
using RabiRiichi.Server.Services;
using RabiRiichi.Server.Generated.Rpc;
using System;

namespace RabiRiichi.Tests.Server.Services {
  [TestClass]
  public class RoomServiceImplTest {
    private RoomServiceImpl service;
    private RoomList roomList;
    private Random rand;

    [TestInitialize]
    public void SetUp() {
      rand = new Random(0);
      roomList = new RoomList(rand);
      // Construct service with null dependencies, as JoinRoom doesn't use them.
      service = new RoomServiceImpl(null, null, rand, null, null);
    }

    [TestMethod]
    public void TestJoinRoom_Success() {
      var room = new Room(rand, new GameConfig { playerCount = 4 }) { roomList = roomList };
      roomList.Add(room);

      var user = new User { id = 1, nickname = "Player1" };

      var request = new JoinRoomRequest { RoomId = room.id };
      var response = service.JoinRoom(request, roomList, user);

      Assert.IsNotNull(response);
      Assert.IsNotNull(response.State);
      Assert.AreEqual(room.id, response.State.Id);
      Assert.IsTrue(room.players.Contains(user));
      Assert.AreEqual(room, user.room);
    }

    [TestMethod]
    public void TestJoinRoom_AlreadyInSameRoom_ReturnsSuccessState() {
      var room = new Room(rand, new GameConfig { playerCount = 4 }) { roomList = roomList };
      roomList.Add(room);

      var user = new User { id = 1, nickname = "Player1" };
      Assert.IsTrue(room.AddPlayer(user));

      var request = new JoinRoomRequest { RoomId = room.id };
      
      // Before fix, this would throw Unavailable (Room is full)
      var response = service.JoinRoom(request, roomList, user);

      Assert.IsNotNull(response);
      Assert.IsNotNull(response.State);
      Assert.AreEqual(room.id, response.State.Id);
      Assert.IsTrue(room.players.Contains(user));
      Assert.AreEqual(room, user.room);
    }

    [TestMethod]
    public void TestJoinRoom_AlreadyInDifferentRoom_ThrowsFailedPrecondition() {
      var room1 = new Room(rand, new GameConfig { playerCount = 4 }) { roomList = roomList };
      var room2 = new Room(rand, new GameConfig { playerCount = 4 }) { roomList = roomList };
      roomList.Add(room1);
      roomList.Add(room2);

      var user = new User { id = 1, nickname = "Player1" };
      Assert.IsTrue(room1.AddPlayer(user));

      var request = new JoinRoomRequest { RoomId = room2.id };

      var ex = Assert.ThrowsException<RpcException>(() => service.JoinRoom(request, roomList, user));
      Assert.AreEqual(StatusCode.FailedPrecondition, ex.Status.StatusCode);
      Assert.AreEqual("Player is already in another room", ex.Status.Detail);
    }

    [TestMethod]
    public void TestJoinRoom_RoomFull_ThrowsResourceExhausted() {
      var room = new Room(rand, new GameConfig { playerCount = 1 }) { roomList = roomList };
      roomList.Add(room);

      var user1 = new User { id = 1, nickname = "Player1" };
      Assert.IsTrue(room.AddPlayer(user1));

      var user2 = new User { id = 2, nickname = "Player2" };
      var request = new JoinRoomRequest { RoomId = room.id };

      var ex = Assert.ThrowsException<RpcException>(() => service.JoinRoom(request, roomList, user2));
      // Should throw ResourceExhausted rather than Unavailable
      Assert.AreEqual(StatusCode.ResourceExhausted, ex.Status.StatusCode);
      Assert.AreEqual("Room is full", ex.Status.Detail);
    }
  }
}
