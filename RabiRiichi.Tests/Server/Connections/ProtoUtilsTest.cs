using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using Google.Protobuf.WellKnownTypes;
using System;

namespace RabiRiichi.Tests.Server.Connections {
  [TestClass]
  public class ProtoUtilsTest {
    [TestMethod]
    public void TestCreateServerResponse() {
      var getInfoResp = new GetInfoResponse();
      var resp1 = ProtoUtils.CreateServerResponse(getInfoResp);
      Assert.AreEqual(getInfoResp, resp1.GetInfo);

      var createUserResp = new CreateUserResponse();
      var resp2 = ProtoUtils.CreateServerResponse(createUserResp);
      Assert.AreEqual(createUserResp, resp2.CreateUser);

      var empty = new Empty();
      var resp3 = ProtoUtils.CreateServerResponse(empty);
      // Empty should not throw exception.

      // Test invalid type
      Assert.ThrowsException<ArgumentException>(() => ProtoUtils.CreateServerResponse(new Timestamp()));
    }

    [TestMethod]
    public void TestCreateServerMsg() {
      var hb = new TwoWayHeartBeatMsg();
      var msg1 = ProtoUtils.CreateServerMsg(hb);
      Assert.AreEqual(hb, msg1.HeartBeatMsg);

      var roomState = new ServerRoomStateMsg();
      var msg2 = ProtoUtils.CreateServerMsg(roomState);
      Assert.AreEqual(roomState, msg2.RoomStateMsg);

      Assert.ThrowsException<ArgumentException>(() => ProtoUtils.CreateServerMsg(new Timestamp()));
    }

    [TestMethod]
    public void TestCreateDto() {
      // Case 1: ServerMsg type (TwoWayHeartBeatMsg)
      var hb = new TwoWayHeartBeatMsg();
      var dto1 = ProtoUtils.CreateDto(hb, 1);
      Assert.AreEqual(1, dto1.RespondTo);
      Assert.IsNotNull(dto1.ServerMsg);
      Assert.AreEqual(hb, dto1.ServerMsg.HeartBeatMsg);
      Assert.IsNull(dto1.ServerResp);

      // Case 2: ServerResponse type (GetInfoResponse)
      var getInfoResp = new GetInfoResponse();
      var dto2 = ProtoUtils.CreateDto(getInfoResp, 2);
      Assert.AreEqual(2, dto2.RespondTo);
      Assert.IsNotNull(dto2.ServerResp);
      Assert.AreEqual(getInfoResp, dto2.ServerResp.GetInfo);
      Assert.IsNull(dto2.ServerMsg);

      // Case 3: Invalid type
      Assert.ThrowsException<ArgumentException>(() => ProtoUtils.CreateDto(new Timestamp()));
    }
  }
}
