using System;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Models;

namespace DeviceWifiToMosquitto.Interfaces
{
    public interface IPowerPilotPublisher
    {
        public void Publish(string deviceEUI, PowerpilotProtoMessage message);
    }
}
