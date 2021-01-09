using System;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Interfaces;
using DeviceWifiToMosquitto.Models;

namespace DeviceWifiToMosquitto.Tests.Mocks
{
    public class MockPublisher : IPowerPilotPublisher
    {
        public event EventHandler<PowerpilotProtoMessage> OnPublish;

        public MockPublisher()
        {
        }

        public void Publish(string deviceEUI, PowerpilotProtoMessage message)
        {
            //Console.WriteLine($"Publishing message {message}");
            OnPublish?.Invoke(this, message);
        }
    }
}
