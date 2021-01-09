using System;
using DeviceWifiToMosquitto.Interfaces;
using DeviceWifiToMosquitto.Models;

namespace DeviceWifiToMosquitto.Tests.Mocks
{
    public class MockSubscriber : IPowerPilotSubscriber
    {
        public MockSubscriber()
        {
        }

        public event EventHandler<ReceivedMessageArgs> MessageReceived;

        public void InjectMessage(string payload)
        {
            var args = new ReceivedMessageArgs(payload);
            MessageReceived?.Invoke(this, args);
        }

    }
}
