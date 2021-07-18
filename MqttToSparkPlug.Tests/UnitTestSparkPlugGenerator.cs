using System;
using MqttToSparkPlug.Services;
using MqttToSparkPlug.Tests.Mocks;
using Xunit;
using Google.Protobuf;
using System.Threading;
using org.eclipse.tahu.protobuf;
using System.Linq;
using PowerPilotCommon.Models.MQTT;
using PowerPilotCommon.Models.SparkPlug;

namespace MqttToSparkPlug.Tests
{
    public class UnitTestSparkPlugGenerator
    {
        private const string publishTopic = "spBv1.0/DTX/{0}/PowerPilotCS/{1}";

        [Fact]
        public void Test_NBIRTH()
        {
            var spg = new SparkPlugGenerator(new MockLogger(), new MockDataService(), publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();

            inputMqtt.MQTTConnected += spg.OnMQTTClientConnected;
            spg.MessageProcessed += outputMqtt.Publish;

            // mock connection event
            inputMqtt.Connect();

            // expect NBIRTH message to be published
            Assert.Equal(1, outputMqtt.GetNumPublishedMessages());

            var receivedMessages = outputMqtt.GetMessagesReceived();
            Assert.Equal("spBv1.0/DTX/NBIRTH/PowerPilotCS", receivedMessages[0].Topic);
            //var payload = Payload.Parser.ParseFrom(lastMessage.Payload);
            //Assert.Equal((ulong)1, payload.Seq);
        }

        public void OnMessagePublished(object sender, MQTTMessageArgs args)
        {
            Console.WriteLine(args);
        }

        [Fact]
        // Test that a DBIRTH can be sent if there are NO existing messages in the db
        public void Test_DBIRTH_NullValues()
        {
            var spg = new SparkPlugGenerator(new MockLogger(), new MockDataService(), publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var energyMsg = new Ppuplink.EnergyMessage()
            {
                Deviceeui = deviceEUI,
                Phaseid = 1,
                Timesent = 1612234855,
                Energyexportreactive = 1,
                Energyexportreal = 1,
                Energyimportreactive = 1,
                Energyimportreal = 1
            };

            // trigger DBIRTH
            var topic = $"application/powerpilot/uplink/energy/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, energyMsg.ToByteArray()));

            Thread.Sleep(10);

            var messages = outputMqtt.GetMessagesReceived();

            // expect DBIRTH
            var dbirth = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DBIRTH}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(dbirth);
            var payload = Payload.Parser.ParseFrom(dbirth.Payload);
            Assert.Equal(53, payload.Metrics.Count);

            // expect energy
            var energy = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(energy);
            payload = Payload.Parser.ParseFrom(energy.Payload);
            Assert.Equal(4, payload.Metrics.Count);

            Assert.Equal(2, outputMqtt.GetNumPublishedMessages());
        }
        
        [Fact]
        // Test that a DBIRTH can be sent if there ARE existing messages in the db
        public void Test_DBIRTH_ExistingValues()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var energyMsg = new Ppuplink.EnergyMessage()
            {
                Deviceeui = deviceEUI,
                Phaseid = 1,
                Timesent = 1612234855,
                Energyexportreactive = 1,
                Energyexportreal = 1,
                Energyimportreactive = 1,
                Energyimportreal = 1
            };
            dataService.Energy = energyMsg;

            var pqMsg = new Ppuplink.PQMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Currentmax = 10,
                Voltagemax = 250.0,
                Phaseid = 1
            };
            dataService.PQ = pqMsg;

            var uplinkMsg = new Ppuplink.UplinkMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Fctn = 1,
                Messageid = 1,
                Rssi = 10,
                Snr = -10,
                Frequency = 8000,
                Messagetype = 32
            };
            dataService.Uplink = uplinkMsg;

            var voltageStats = new Ppuplink.VoltageStatsMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Starttime = "2021/01/01",
                Stoptime = "2021/01/02",
                H0213 = 1,
                H213215 = 1
            };
            dataService.Uplink = uplinkMsg;

            // trigger DBIRTH and energy
            var topic = $"application/powerpilot/uplink/energy/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, energyMsg.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect DBIRTH
            var dbirth = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DBIRTH}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(dbirth);
            var payload = Payload.Parser.ParseFrom(dbirth.Payload);
            Assert.Equal(53, payload.Metrics.Count);

            // expect energy
            var energy = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(energy);
            payload = Payload.Parser.ParseFrom(energy.Payload);
            Assert.Equal(4, payload.Metrics.Count);

            Assert.Equal(2, outputMqtt.GetNumPublishedMessages());
        }

        
        [Fact]
        // Simulate service starting up and receiving messages
        public void Test_NBIRTH_DBIRTH_Energy()
        {
            var spg = new SparkPlugGenerator(new MockLogger(), new MockDataService(), publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MQTTConnected += spg.OnMQTTClientConnected;
            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            // trigger NBIRTH
            inputMqtt.Connect();

            var energyMsg = new Ppuplink.EnergyMessage()
            {
                Deviceeui = deviceEUI,
                Phaseid = 1,
                Timesent = 1612234855,
                Energyexportreactive = 1,
                Energyexportreal = 1,
                Energyimportreactive = 1,
                Energyimportreal = 1
            };

            // trigger DBIRTH and energy
            var topic = $"application/powerpilot/uplink/energy/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, energyMsg.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect NBIRTH
            var nbirth = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.NBIRTH}/PowerPilotCS");
            Assert.NotNull(nbirth);

            //expect DBIRTH
            var dbirth = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DBIRTH}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(dbirth);
            var payload = Payload.Parser.ParseFrom(dbirth.Payload);
            Assert.Equal(53, payload.Metrics.Count);

            //expect energy
            var energy = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(energy);
            payload = Payload.Parser.ParseFrom(energy.Payload);
            Assert.Equal(4, payload.Metrics.Count);

            Assert.Equal(3, outputMqtt.GetNumPublishedMessages());

        }
        
        [Fact]
        public void Test_PQ()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var pqMsg = new Ppuplink.PQMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Currentmax = 10,
                Voltagemax = 250.0,
                Phaseid = 1
            };
            dataService.PQ = pqMsg;

            // trigger DBIRTH and PQ
            var topic = $"application/powerpilot/uplink/pq/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, pqMsg.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect pq
            var pq = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(pq);
            var payload = Payload.Parser.ParseFrom(pq.Payload);
            Assert.Equal(17, payload.Metrics.Count);

        }

        
        [Fact]
        public void Test_VoltageStats()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var voltageStats = new Ppuplink.VoltageStatsMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Starttime = "2021/01/01",
                Stoptime = "2021/01/02",
                Phaseid = 1,
                H0213 = 1,
                H213215 = 1,
                H215217 = 0,
                H217219 = 10,
                H219221 = 2,
                H221223 = 5,
                H223225 = 0,
                H225227 = 1,
                H227229 = 4,
                H229231 = 0,
                H231233 = 12,
                H233235 = 500,
                H235237 = 2,
                H237239 = 0,
                H239241 = 2,
                H241243 = 0,
                H243245 = 0,
                H245247 = 0,
                H247249 = 0,
                H249300 = 1,
                Mean = 12.4,
                Variance = 234.6
            };

            // trigger DBIRTH and voltagestats
            var topic = $"application/powerpilot/uplink/voltagestats/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, voltageStats.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect voltagestats
            var voltagestats = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(voltagestats);
            var payload = Payload.Parser.ParseFrom(voltagestats.Payload);
            Assert.Equal(24, payload.Metrics.Count);

        }
        
        [Fact]
        public void Test_Uplink()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var uplinkMsg = new Ppuplink.UplinkMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Fctn = 1,
                Messageid = 1,
                Rssi = 10,
                Snr = -10,
                Frequency = 8000,
                Messagetype = 32,
                Dr = 200,
                Rawdata = "0A86454EFA",
                Resent = 0
            };
            dataService.Uplink = uplinkMsg;

            // trigger DBIRTH and uplink
            var topic = $"application/powerpilot/uplink/uplink/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, uplinkMsg.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect uplink
            var uplink = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(uplink);
            var payload = Payload.Parser.ParseFrom(uplink.Payload);
            Assert.Equal(4, payload.Metrics.Count);

        }

        [Fact]
        public void Test_Alarm_PowerFail()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "powerfailalarm",
                Phaseid = 1,
                Value = 0
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.POWER_FAIL}", payload.Metrics[0].Name);

            // expect DDEATH
            var ddeath = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDEATH}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(ddeath);
        }

        [Fact]
        public void Test_Alarm_HighVoltage()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "highvoltagealarm",
                Phaseid = 1,
                Value = 257.32
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.VOLTAGE_HIGH}", payload.Metrics[0].Name);
        }

        [Fact]
        public void Test_Alarm_VeryHighVoltage()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "veryhighvoltagealarm",
                Phaseid = 1,
                Value = 267.32
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.VOLTAGE_HIGH}", payload.Metrics[0].Name);
        }

        [Fact]
        public void Test_Alarm_LowVoltage()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "lowvoltagealarm",
                Phaseid = 1,
                Value = 205.32
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.VOLTAGE_LOW}", payload.Metrics[0].Name);
        }

        [Fact]
        public void Test_Alarm_VeryLowVoltage()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "verylowvoltagealarm",
                Phaseid = 1,
                Value = 194.32
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.VOLTAGE_LOW}", payload.Metrics[0].Name);
        }

        [Fact]
        public void Test_Alarm_NormalVoltage()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            var alarmMessage = new Ppuplink.AlarmMessage()
            {
                Deviceeui = deviceEUI,
                Timesent = 1612234855,
                Alarmtype = "normalvoltage",
                Phaseid = 1,
                Value = 237.3
            };

            // trigger DBIRTH and alarm
            var topic = $"application/powerpilot/uplink/alarm/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, alarmMessage.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect alarm
            var alarm = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(alarm);
            var payload = Payload.Parser.ParseFrom(alarm.Payload);
            Assert.Single(payload.Metrics);
            Assert.Equal($"{Alarm.TYPE}/1/{Alarm.VOLTAGE_NORMAL}", payload.Metrics[0].Name);

            // should have extra DBIRTH for coming back to life
            Assert.Equal(3, outputMqtt.GetNumPublishedMessages());
        }

        [Fact]
        public void Test_REBIRTH()
        {
            var dataService = new MockDataService();
            var spg = new SparkPlugGenerator(new MockLogger(), dataService, publishTopic);
            var inputMqtt = new MockMQTTWrapper();
            var outputMqtt = new MockMQTTWrapper();
            var deviceEUI = "123";

            inputMqtt.MessageReceived += spg.ProcessMessage;
            spg.MessageProcessed += outputMqtt.Publish;
            outputMqtt.MessagePublished += OnMessagePublished;

            // trigger initial DBIRTH
            var energyMsg = new Ppuplink.EnergyMessage()
            {
                Deviceeui = deviceEUI,
                Phaseid = 1,
                Timesent = 1612234855,
                Energyexportreactive = 1,
                Energyexportreal = 1,
                Energyimportreactive = 1,
                Energyimportreal = 1
            };

            // trigger DBIRTH and energy
            var topic = $"application/powerpilot/uplink/energy/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, energyMsg.ToByteArray()));

            Thread.Sleep(10);
            var messages = outputMqtt.GetMessagesReceived();

            // expect energy
            var energy = messages.Find(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}");
            Assert.NotNull(energy);
            var payload = Payload.Parser.ParseFrom(energy.Payload);
            Assert.Equal(4, payload.Metrics.Count);

            // send REBIRTH cmd
            var metric = new Payload.Types.Metric()
            {
                Name = $"{DeviceCommands.TYPE}/{DeviceCommands.REBIRTH}",
                Timestamp = 1612234855
            };

            Payload cmdPayload = new Payload()
            {
                Seq = 1,
                Timestamp = (ulong)DateTimeOffset.Now.ToUnixTimeSeconds(),
            };
            cmdPayload.Metrics.Add(metric);

            topic = $"spBv1.0/DTX/{SparkPlugMessageTypes.DCMD}/PowerPilotCS/{deviceEUI}";
            inputMqtt.InjectMessage(new MQTTMessageArgs(topic, cmdPayload.ToByteArray()));

            Thread.Sleep(10);
            messages = outputMqtt.GetMessagesReceived();

            // expect second DBIRTH
            var dbirths = messages.Where(m => m.Topic == $"spBv1.0/DTX/{SparkPlugMessageTypes.DBIRTH}/PowerPilotCS/{deviceEUI}");
            Assert.Equal(2, dbirths.Count());

        }
    }
}
