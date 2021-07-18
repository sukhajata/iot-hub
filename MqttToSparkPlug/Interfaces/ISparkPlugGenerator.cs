using System;
using PowerPilotCommon.Models.MQTT;

namespace MqttToSparkPlug.Interfaces
{
    public interface ISparkPlugGenerator
    {
        event EventHandler<MQTTMessageArgs> MessageProcessed;

        public void ProcessMessage(object sender, MQTTMessageArgs args);
    }
}
