using System;
using System.Threading.Tasks;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Interfaces;
using DeviceWifiToMosquitto.Models;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace DeviceWifiToMosquitto.Services
{
    public class MosquittoPublisher : IPowerPilotPublisher
    {
        private IManagedMqttClient _mqttClient;
        private ILoggerService _loggerService;

        public MosquittoPublisher(IManagedMqttClient mqttClient, ILoggerService loggerService)
        {
            _mqttClient = mqttClient;
            _loggerService = loggerService;
        }

        public void Publish(string deviceEUI, PowerpilotProtoMessage protoMessage)
        {
            var uplinkTopic = $"application/powerpilot/uplink/{protoMessage.Type}/{deviceEUI}";

            Task.Run(() => _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(uplinkTopic)
                    .WithPayload(protoMessage.Value.ToByteArray())
                    .WithAtLeastOnceQoS()
                    .Build()));

            //send timesync downlink
            if (protoMessage.Type == "timesync")
            {
                var downlinkTopic = $"application/powerpilot/downlink/{protoMessage.Type}/{deviceEUI}";

                Task.Run(() => _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                        .WithTopic(downlinkTopic)
                        .WithPayload(protoMessage.Value.ToByteArray())
                        .WithAtLeastOnceQoS()
                        .Build()));
            }
        }

    }
}
