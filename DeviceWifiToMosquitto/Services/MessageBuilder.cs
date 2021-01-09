using System;
using System.Collections.Generic;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Interfaces;
using DeviceWifiToMosquitto.Models;
using Google.Protobuf;
using Grpc.Core;
using Ppuplink;

namespace DeviceWifiToMosquitto.Services
{

    public class MessageBuilder : IMessageBuilder
    {
        private BinaryProtocol.BinaryProtocolClient _binaryServiceClient;
        private ILoggerService _loggerService;
        private IPowerPilotPublisher _publisher;

        public MessageBuilder(BinaryProtocol.BinaryProtocolClient binaryServiceClient, ILoggerService loggerService, IPowerPilotPublisher publisher)
        {
            _binaryServiceClient = binaryServiceClient;
            _loggerService = loggerService;
            _publisher = publisher;
        }

        private static byte[] String2Byte(String hexString)
        {
            List<byte> data = new List<byte>();
            for (int i = 0; i < hexString.Length; i += 2)
            {
                string hs = hexString.Substring(i, 2);
                data.Add(Convert.ToByte(hs, 16));
            }
            return data.ToArray();
        }

        public async void BuildMessage(object sender, ReceivedMessageArgs a)
        {
            try
            {
                _loggerService.LogMessage($"received message {a.Payload}");
                DeviceWifiMessage msg = DeviceWifiMessage.FromJson(a.Payload);
                if (msg == null)
                {
                    _loggerService.LogMessage("got nothing");
                    return;
                }
                else
                {
                    string deviceEUI = msg.DeviceEUI.Trim();
                    if (msg.Data != null)
                    {
                        byte[] data = String2Byte(msg.Data);
                        var request = new BinaryProtocolService.DecodeRequest()
                        {
                            Data = ByteString.CopyFrom(data),
                            DeviceEUI = deviceEUI
                        };
                        var decodeResponse = await _binaryServiceClient.DecodeAsync(request);
                        
                        List<PowerpilotProtoMessage> protoMessages = new List<PowerpilotProtoMessage>();
                        protoMessages.AddRange(decodeResponse.Messages);

                        var uplink = new UplinkMessage()
                        {
                            Deviceeui = deviceEUI,
                            Snr = 0,
                            Fctn = 0,
                            Dr = 0,
                            Frequency = 0,
                            Rawdata = msg.Data,
                            Timesent = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            Messagetype = Convert.ToUInt16(msg.Data.Substring(0, 2), 16),
                            Messageid = Convert.ToUInt16(msg.Data.Substring(2, 2), 16),
                            Resent = (uint)((Convert.ToUInt16(msg.Data.Substring(4, 2), 16) & 0x0040) == 0x0040 ? 1 : 0)
                        };
                        protoMessages.Add(new PowerpilotProtoMessage()
                        {
                            Type = "uplink",
                            Value = ByteString.CopyFrom(uplink.ToByteArray())
                        });

                        foreach (PowerpilotProtoMessage item in protoMessages)
                        {
                            _loggerService.LogMessage($"Publishing message {item.Type}");
                            _publisher.Publish(deviceEUI, item);
                        }
                    }

                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.Internal)
            {
                _loggerService.LogError("Failed to call grpc service: " + ex.Message, "MessageReceived", Pplogger.ErrorMessage.Types.Severity.Fatal);
                //Can not connect to binary service, better die
                Liveness.LIVE = false;
                return;
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex.Message + ":" + ex.StackTrace, "MessageReceived");
            }
        }
    }
}
