using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DeviceWifiToMosquitto.Models;
using MQTTnet.Extensions.ManagedClient;
using Ppuplink;

using BinaryProtocolService;
using Grpc.Core;
using DeviceWifiToMosquitto.Interfaces;

namespace DeviceWifiToMosquitto
{
    public class MosquittoSubscriber : IPowerPilotSubscriber
    {
        public event EventHandler<ReceivedMessageArgs> MessageReceived;

        private IManagedMqttClient _mqttClient;
        private ILoggerService _loggerService;

        public MosquittoSubscriber(IManagedMqttClient mqttClient, ILoggerService loggerService, string uplinkTopic)
        {
            _loggerService = loggerService;
            _mqttClient = mqttClient;

            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                try
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    _loggerService.LogMessage($"Topic: {topic}. Message Received: {payload}");

                    var args = new ReceivedMessageArgs(payload);
                    Task.Run(() => MessageReceived?.Invoke(this, args));
                }
                catch (Exception ex)
                {
                    _loggerService.LogError(ex.Message, "MosquittoSubscriber");
                }
            });

            Task.Run(() => _mqttClient.SubscribeAsync(uplinkTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce));

        }


    }
}
