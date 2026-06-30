using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RabiRiichi.Server.Connections;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;
using Grpc.Core;

namespace RabiRiichi.Tests.Server.Connections {
  [TestClass]
  public class ConnectionTest {
    [TestMethod]
    public async Task TestConnectionSendReceiveAndHeartbeat() {
      var mockReader = new Mock<IAsyncStreamReader<ClientMessageDto>>();
      var mockWriter = new Mock<IServerStreamWriter<ServerMessageDto>>();

      var clientMessages = new Queue<ClientMessageDto>();
      var MoveNextTcs = new TaskCompletionSource<bool>();

      mockReader.Setup(r => r.MoveNext(It.IsAny<CancellationToken>()))
        .Returns(() => {
          lock (clientMessages) {
            if (clientMessages.Count > 0) {
              return Task.FromResult(true);
            }
            if (MoveNextTcs.Task.IsCompleted) {
              MoveNextTcs = new TaskCompletionSource<bool>();
            }
            return MoveNextTcs.Task;
          }
        });

      mockReader.Setup(r => r.Current)
        .Returns(() => {
          lock (clientMessages) {
            return clientMessages.Dequeue();
          }
        });

      var writtenMessages = new List<ServerMessageDto>();
      var writeTcs = new TaskCompletionSource<bool>();

      mockWriter.Setup(w => w.WriteAsync(It.IsAny<ServerMessageDto>(), It.IsAny<CancellationToken>()))
        .Callback<ServerMessageDto, CancellationToken>((dto, token) => {
          lock (writtenMessages) {
            writtenMessages.Add(dto);
          }
          writeTcs.TrySetResult(true);
        })
        .Returns(Task.CompletedTask);

      // Enqueue first message BEFORE connecting
      var hbMsg = new ClientMessageDto {
        Id = 1,
        ClientMsg = new ClientMsg {
          HeartBeatMsg = new TwoWayHeartBeatMsg { MaxId = 0 }
        }
      };
      lock (clientMessages) {
        clientMessages.Enqueue(hbMsg);
      }

      using var connection = new Connection();
      
      ClientMessageDto receivedDto = null;
      var receiveTcs = new TaskCompletionSource<bool>();
      connection.OnReceive += dto => {
        receivedDto = dto;
        receiveTcs.TrySetResult(true);
      };

      var ctx = connection.Connect(mockReader.Object, mockWriter.Object);

      // Wait for receive to process first message
      await receiveTcs.Task;
      
      Assert.IsNotNull(receivedDto);
      Assert.AreEqual(1, receivedDto.Id);

      // Heartbeat should trigger a response
      await writeTcs.Task;
      
      lock (writtenMessages) {
        Assert.AreEqual(1, writtenMessages.Count);
        Assert.AreEqual(-1, writtenMessages[0].Id);
        Assert.AreEqual(1, writtenMessages[0].RespondTo);
        Assert.IsNotNull(writtenMessages[0].ServerMsg?.HeartBeatMsg);
      }

      // Reset writeTcs for next write
      writeTcs = new TaskCompletionSource<bool>();

      // Test sending a message from server to client
      var serverMsg = new ServerMessageDto { Id = 10, ServerMsg = new ServerMsg() };
      var wrapper = connection.Queue(serverMsg);
      
      // Wait for SendMsgLoop to write it
      await writeTcs.Task;

      lock (writtenMessages) {
        Assert.AreEqual(2, writtenMessages.Count);
        Assert.AreEqual(wrapper.msg.Id, writtenMessages[1].Id);
      }

      // Test receiving another message dynamically
      receiveTcs = new TaskCompletionSource<bool>();
      var userMsg = new ClientMessageDto {
        Id = 2,
        ClientMsg = new ClientMsg {
          // Some other message
        }
      };
      lock (clientMessages) {
        clientMessages.Enqueue(userMsg);
      }
      
      // Wake up MoveNext
      MoveNextTcs.TrySetResult(true);

      await receiveTcs.Task;
      Assert.AreEqual(2, receivedDto.Id);

      // Clean up: stop receiver loop
      MoveNextTcs.TrySetResult(false);
      
      connection.Close();
    }
  }
}
