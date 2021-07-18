using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet;
using PowerPilotCommon.Interfaces;
using PowerPilotCommon.Models.MQTT;

namespace MqttToSparkPlug.Tests.Mocks
{
    public class MockMQTTWrapper : IMQTTClientWrapper
    {
        private int numPublishedMessages = 0;
        private List<MQTTMessageArgs> receivedMessages = new List<MQTTMessageArgs>();

        public MockMQTTWrapper()
        {
        }

        public event EventHandler<MQTTMessageArgs> MessageReceived;
        public event EventHandler MQTTConnected;
        public event EventHandler MQTTDisconnected;
        public event EventHandler<MQTTMessageArgs> MessagePublished;

        public void Publish(object sender, MQTTMessageArgs args)
        {
            numPublishedMessages++;
            receivedMessages.Add(args);
            MessagePublished?.Invoke(this, args);
        }

        public Task Subscribe(string topic)
        {
            return null;
        }

        public void Connect()
        {
            MQTTConnected?.Invoke(this, new EventArgs());
        }

        public void InjectMessage(MQTTMessageArgs args)
        {
            MessageReceived?.Invoke(this, args);
        }

        public int GetNumPublishedMessages()
        {
            return numPublishedMessages;
        }

        public List<MQTTMessageArgs> GetMessagesReceived()
        {
            return receivedMessages;
        }

        public void Publish(object sender, MqttApplicationMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
