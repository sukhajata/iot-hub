using System;
using DeviceWifiToMosquitto.Models;

namespace DeviceWifiToMosquitto.Interfaces
{
    public interface IMessageBuilder
    {
        public void BuildMessage(object sender, ReceivedMessageArgs a);

    }
}
