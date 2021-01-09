using System;
using DeviceWifiToMosquitto.Models;

namespace DeviceWifiToMosquitto.Interfaces
{
    public interface IPowerPilotSubscriber
    {
        event EventHandler<ReceivedMessageArgs> MessageReceived;

    }
}
