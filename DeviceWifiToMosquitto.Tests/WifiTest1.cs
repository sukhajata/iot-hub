using System;
using System.Threading;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Interfaces;
using DeviceWifiToMosquitto.Services;
using DeviceWifiToMosquitto.Tests.Mocks;
using Grpc.Core;
using Grpc.Net.Client;
using Xunit;
using Google.Protobuf;

namespace DeviceWifiToMosquitto.Tests
{
    public class WifiTest1
    {
        private string result;


        public void MessagePublished(object sender, PowerpilotProtoMessage message)
        {
            Console.WriteLine(message);
            result = message.Value.ToBase64();
        }

        [Fact]
        public void Test1()
        {
            try
            {
                //SETUP
                Console.WriteLine("setting up");
                var expected = "ACCAX5kpsFUtAdwh9Qao";

                //grpc binary protocol service
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); //allow insecure (within cluster)
                var channel = GrpcChannel.ForAddress("http://localhost:5000");
                var binaryServiceClient = new BinaryProtocol.BinaryProtocolClient(channel);

                //mock binary protocol service with default response
               /* var message = new PowerpilotProtoMessage()
                {
                    Type = "inst",
                    Value = ByteString.FromBase64(expected)
                };
                var response = new DecodeResponse();
                response.Messages.Add(message);
                var mockClient = new Moq.Mock<BinaryProtocol.BinaryProtocolClient>();
                mockClient.Setup(m => m.Decode(Moq.It.IsAny<DecodeRequest>(), Moq.It.IsAny<CallOptions>())).Returns(response);
               */

                //dependencies
                var subscriber = new MockSubscriber();

                var publisher = new MockPublisher();
                publisher.OnPublish += MessagePublished;

                var logger = new MockLoggerService();

                var messageBuilder = new MessageBuilder(binaryServiceClient, logger, publisher);
                subscriber.MessageReceived += messageBuilder.BuildMessage;

                //START
                Console.WriteLine("starting test");
                var data = "{ \"deveui\": \"123345\", \"data\": \"0020805f9929b0552d01dc21f506a8\"}";
                //var data = "{ \"deveui\": \"123345\", \"data\": \"0020805fcf78c60000000000000000\"}";
                
                subscriber.InjectMessage(data);

                Thread.Sleep(2000);

                Console.WriteLine("finished");
                Assert.Equal(expected, result);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Assert.Equal("0", "1");
            }
        }
    }
}
