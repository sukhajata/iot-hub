using System;
using MqttToSparkPlug.Interfaces;
using Google.Protobuf;
using System.Collections.Generic;
using System.Threading.Tasks;
using PowerPilotCommon.Interfaces;
using PowerPilotCommon.Models.MQTT;
using PowerPilotCommon.Services;
using PowerPilotCommon.Models.SparkPlug;
using org.eclipse.tahu.protobuf;
using System.Threading;

namespace MqttToSparkPlug.Services
{
    public class SparkPlugGenerator : ISparkPlugGenerator
    {
        public event EventHandler<MQTTMessageArgs> MessageProcessed;

        private readonly ILoggerClient _loggerClient;
        private readonly IDataServiceClient _dataService;
        private readonly string _publishTopic;
        private readonly object dbirthLock = new object();

        private bool nbirth_sent = false;

        //keep track of which devices we have seen
        private List<string> deviceEUIs = new List<string>();

        //this is the seq for DDATA messages which must be between 0 and 255 and must increment for each message 
        private int seq = 0;

        public SparkPlugGenerator(ILoggerClient loggerClient, IDataServiceClient dataService, string publishTopic)
        {
            _loggerClient = loggerClient;
            _dataService = dataService;
            _publishTopic = publishTopic;
        }

        private ulong GetSeq()
        {
            seq++;
            if (seq >= 256)
            {
                seq = 0;
            }
            return (ulong)seq;
        }

        private int GetPhase(uint line, PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            if (line == 1 && connection.Line1.HasValue)
            {
                return connection.Line1.Value;
            }
            if (line == 2 && connection.Line2.HasValue)
            {
                return connection.Line2.Value;
            }
            if (line == 3 && connection.Line3.HasValue)
            {
                return connection.Line3.Value;
            }

            throw new Exception($"Could not find phase for line {line}");
        }

        private Payload.Types.Metric GetMetric(string messageType, string phaseID, string name, ulong timestamp)
        {
            string metricName;
            if (phaseID == "")
            {
                metricName = $"{messageType}/{name}";
            }
            else
            {
                metricName = $"{messageType}/{phaseID}/{name}";
            }

            return new Payload.Types.Metric()
            {
                Name = metricName,
                Timestamp = timestamp
            };
        }

        private Payload.Types.Metric GetMetricDouble(string messageType, string phaseID, string name, double? value, ulong timestamp)
        {
            var metric = GetMetric(messageType, phaseID, name, timestamp);
            metric.Datatype = Payload.Types.Metric.DoubleValueFieldNumber;
            if (value.HasValue)
            {
                metric.DoubleValue = value.Value;
            }
            else
            {
                metric.IsNull = true;
            }
            return metric;
        }

        private Payload.Types.Metric GetMetricInt(string messageType, string phaseID, string name, uint? value, ulong timestamp)
        {
            var metric = GetMetric(messageType, phaseID, name, timestamp);
            metric.Datatype = Payload.Types.Metric.IntValueFieldNumber;
            if (value.HasValue)
            {
                metric.IntValue = value.Value;
            }
            else
            {
                metric.IsNull = true;
            }
            return metric;
        }

        private Payload.Types.Metric GetMetricLong(string messageType, string phaseID, string name, ulong? value, ulong timestamp)
        {
            var metric = GetMetric(messageType, phaseID, name, timestamp);
            metric.Datatype = Payload.Types.Metric.LongValueFieldNumber;
            if (value.HasValue)
            {
                metric.LongValue = value.Value;
            }
            else
            {
                metric.IsNull = true;
            }
            return metric;
        }

        private Payload.Types.Metric GetMetricString(string messageType, string phaseID, string name, string value, ulong timestamp)
        {
            var metric = GetMetric(messageType, phaseID, name, timestamp);
            metric.Datatype = Payload.Types.Metric.StringValueFieldNumber;
            if (value != null)
            {
                metric.StringValue = value;
            }
            else
            {
                metric.IsNull = true;
            }
            return metric;
        }

        private Payload AddEnergyMetrics(Ppuplink.EnergyMessage energyMessage, Payload payload)
        {
            //no phase for energy message
            var phaseID = "";
            ulong timestamp;
            if (energyMessage == null)
            {
                timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now);
            }
            else
            {
                timestamp = energyMessage.Timesent;
            }

            payload.Metrics.Add(GetMetricDouble(Energy.TYPE, phaseID, Energy.IMPORT_ACTIVE, energyMessage?.Energyimportreal, timestamp));
            payload.Metrics.Add(GetMetricDouble(Energy.TYPE, phaseID, Energy.IMPORT_REACTIVE, energyMessage?.Energyimportreactive, timestamp));
            payload.Metrics.Add(GetMetricDouble(Energy.TYPE, phaseID, Energy.EXPORT_ACTIVE, energyMessage?.Energyexportreal, timestamp));
            payload.Metrics.Add(GetMetricDouble(Energy.TYPE, phaseID, Energy.EXPORT_REACTIVE, energyMessage?.Energyexportreactive, timestamp));

            return payload;
        }

        private Payload AddPQMetrics(Ppuplink.PQMessage pqMessage, Payload payload, PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            var phaseID = GetPhase(pqMessage.Phaseid, connection).ToString();
            ulong timestamp;
            if (pqMessage == null)
            {
                timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now);
            }
            else
            {
                timestamp = pqMessage.Timesent;
            }

            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.VOLTAGE_MAX, pqMessage?.Voltagemax, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.VOLTAGE_MIN, pqMessage?.Voltagemin, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.VOLTAGE_AVG, pqMessage?.Voltagesma, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.CURRENT_AVG, pqMessage?.Currentsma, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.ACTIVE_POWER_MAX, pqMessage?.Poweractivemax, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.ACTIVE_POWER_MIN, pqMessage?.Poweractivemin, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.ACTIVE_POWER_AVG, pqMessage?.Poweractivesma, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.REACTIVE_POWER_MAX, pqMessage?.Powerreactivemax, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.REACTIVE_POWER_MIN, pqMessage?.Powerreactivemin, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.REACTIVE_POWER_AVG, pqMessage?.Powerreactivesma, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.THDV_MAX, pqMessage?.Thdvmax, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.THDV_MIN, pqMessage?.Thdvmin, timestamp));
            payload.Metrics.Add(GetMetricDouble(PQ.TYPE, phaseID, PQ.THDV_AVG, pqMessage?.Thdvsma, timestamp));

            uint? sag = (uint?)pqMessage?.Momentarysag + (uint?)pqMessage?.Temporarysag;
            uint? swell = (uint?)pqMessage?.Momentaryswell + (uint?)pqMessage?.Temporaryswell;
            uint? underVoltage = (uint?)pqMessage?.Sustainedundervoltage + (uint?)pqMessage?.Prolongedundervoltage;
            uint? overVoltage = (uint?)pqMessage?.Sustainedovervoltage + (uint?)pqMessage?.Prolongedovervoltage;
            payload.Metrics.Add(GetMetricInt(PQ.TYPE, phaseID, PQ.SAG, sag, timestamp));
            payload.Metrics.Add(GetMetricInt(PQ.TYPE, phaseID, PQ.SWELL, swell, timestamp));
            payload.Metrics.Add(GetMetricInt(PQ.TYPE, phaseID, PQ.UNDER_VOLTAGE, underVoltage, timestamp));
            payload.Metrics.Add(GetMetricInt(PQ.TYPE, phaseID, PQ.OVER_VOLTAGE, overVoltage, timestamp));

            return payload;
        }

        private Payload AddVoltageStatsMetrics(Ppuplink.VoltageStatsMessage vs, Payload payload, PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            var phaseID = GetPhase(vs.Phaseid, connection).ToString();
            ulong timestamp;
            if (vs == null)
            {
                timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now);
            }
            else
            {
                timestamp = vs.Timesent;
            }

            payload.Metrics.Add(GetMetricDouble(VoltageStats.TYPE, phaseID, VoltageStats.MEAN, vs?.Mean, timestamp));
            payload.Metrics.Add(GetMetricDouble(VoltageStats.TYPE, phaseID, VoltageStats.VARIANCE, vs?.Variance, timestamp));
            payload.Metrics.Add(GetMetricString(VoltageStats.TYPE, phaseID, VoltageStats.START_TIME, vs?.Starttime, timestamp));
            payload.Metrics.Add(GetMetricString(VoltageStats.TYPE, phaseID, VoltageStats.STOP_TIME, vs?.Stoptime, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V50_213, (ulong?)vs?.H0213, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V213_215, (ulong?)vs?.H213215, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V215_217, (ulong?)vs?.H215217, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V217_219, (ulong?)vs?.H217219, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V219_221, (ulong?)vs?.H219221, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V221_223, (ulong?)vs?.H221223, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V223_225, (ulong?)vs?.H223225, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V225_227, (ulong?)vs?.H225227, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V227_229, (ulong?)vs?.H227229, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V229_231, (ulong?)vs?.H229231, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V231_233, (ulong?)vs?.H231233, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V233_235, (ulong?)vs?.H233235, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V235_237, (ulong?)vs?.H235237, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V237_239, (ulong?)vs?.H237239, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V239_241, (ulong?)vs?.H239241, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V241_243, (ulong?)vs?.H241243, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V243_245, (ulong?)vs?.H243245, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V245_247, (ulong?)vs?.H245247, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V247_249, (ulong?)vs?.H247249, timestamp));
            payload.Metrics.Add(GetMetricLong(VoltageStats.TYPE, phaseID, VoltageStats.V249_300, (ulong?)vs?.H249300, timestamp));

            return payload;
        }

        private Payload AddAlarmMetrics(Ppuplink.AlarmMessage alarmMessage, Payload payload, PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            var phaseID = GetPhase(alarmMessage.Phaseid, connection).ToString();

            if (alarmMessage == null)
            {
                // DBIRTH
                var timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now);
                payload.Metrics.Add(GetMetricInt(Alarm.TYPE, phaseID, Alarm.POWER_FAIL, null, timestamp));
                payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_HIGH, null, timestamp));
                payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_LOW, null, timestamp));
                payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_NORMAL, null, timestamp));

                return payload;
            }

            switch(alarmMessage.Alarmtype)
            {
                case "powerfailalarm":
                    payload.Metrics.Add(GetMetricInt(Alarm.TYPE, phaseID, Alarm.POWER_FAIL, 0, alarmMessage.Timesent));
                    break;

                case "highvoltagealarm":
                case "veryhighvoltagealarm":
                    payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_HIGH, alarmMessage.Value, alarmMessage.Timesent));
                    break;

                case "lowvoltagealarm":
                case "verylowvoltagealarm":
                    payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_LOW, alarmMessage.Value, alarmMessage.Timesent));
                    break;

                case "normalvoltage":
                    payload.Metrics.Add(GetMetricDouble(Alarm.TYPE, phaseID, Alarm.VOLTAGE_NORMAL, alarmMessage.Value, alarmMessage.Timesent));
                    break;

                default:
                    throw new Exception($"Unhandled alarm type {alarmMessage.Alarmtype}");

            }

            return payload;
        }

        private Payload AddUplinkMetrics(Ppuplink.UplinkMessage uplinkMessage, Payload payload)
        {
            ulong timestamp;
            if (uplinkMessage == null)
            {
                timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now);
            }
            else
            {
                timestamp = uplinkMessage.Timesent;
            }

            payload.Metrics.Add(GetMetricInt(Uplink.TYPE, "", Uplink.RSSI, (uint?)uplinkMessage?.Rssi, timestamp));
            payload.Metrics.Add(GetMetricInt(Uplink.TYPE, "", Uplink.SNR, (uint?)uplinkMessage?.Snr, timestamp));
            payload.Metrics.Add(GetMetricLong(Uplink.TYPE, "", Uplink.FREQ, uplinkMessage?.Frequency, timestamp));
            payload.Metrics.Add(GetMetricInt(Uplink.TYPE, "", Uplink.DR, (uint?)uplinkMessage?.Dr, timestamp));

            return payload;
        }

        private Payload BuildDDEATH(Ppuplink.AlarmMessage alarmMessage)
        {
            Payload payload = new Payload()
            {
                Timestamp = alarmMessage.Timesent,
                Seq = GetSeq()
            };

            return payload;
        }

        private async Task<Payload> GetPhaseMessages(PowerPilotCommon.Models.PostgreSQL.Connection connection, Payload payload, uint line)
        {
            int phase = GetPhase(line, connection);
            var pq = await _dataService.GetPQ(connection.DeviceEUI, phase);
            if (pq == null)
            {
                pq = new Ppuplink.PQMessage()
                {
                    Phaseid = line,
                    Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                };
            }
            payload = AddPQMetrics(pq, payload, connection);

            var voltagestats = await _dataService.GetVoltageStats(connection.DeviceEUI, phase);
            if (voltagestats == null)
            {
                voltagestats = new Ppuplink.VoltageStatsMessage()
                {
                    Phaseid = line,
                    Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                };
            }
            payload = AddVoltageStatsMetrics(voltagestats, payload, connection);

            // alarms
            var highvoltage = new Ppuplink.AlarmMessage()
            {
                Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                Phaseid = line,
                Alarmtype = "highvoltagealarm"
            };
            payload = AddAlarmMetrics(highvoltage, payload, connection);

            var lowvoltage = new Ppuplink.AlarmMessage()
            {
                Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                Phaseid = line,
                Alarmtype = "lowvoltagealarm"
            };
            payload = AddAlarmMetrics(lowvoltage, payload, connection);

            var powerfail = new Ppuplink.AlarmMessage()
            {
                Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                Phaseid = line,
                Alarmtype = "powerfailalarm"
            };
            payload = AddAlarmMetrics(powerfail, payload, connection);

            var normalvoltage = new Ppuplink.AlarmMessage()
            {
                Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                Phaseid = line,
                Alarmtype = "normalvoltage"
            };
            payload = AddAlarmMetrics(normalvoltage, payload, connection);

            return payload;
        }

        private async Task<Payload> BuildDBIRTH(PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            Payload payload = new Payload()
            {
                Seq = GetSeq(),
                Timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now)
            };

            if (connection.Line1 > 0)
            {
                payload = await  GetPhaseMessages(connection, payload, 1);
            }

            if (connection.Line2.HasValue && connection.Line2 > 0)
            {
                payload = await GetPhaseMessages(connection, payload, 2);
            }

            if (connection.Line3.HasValue && connection.Line3 > 0)
            {
                payload = await GetPhaseMessages(connection, payload, 3);
            }

            var energy = await _dataService.GetEnergy(connection.DeviceEUI);
            if (energy != null)
            {
                energy = new Ppuplink.EnergyMessage()
                {
                    Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                };
            }
            payload = AddEnergyMetrics(energy, payload);

            var uplink = await _dataService.GetUplink(connection.DeviceEUI);
            if (uplink != null)
            {
                uplink = new Ppuplink.UplinkMessage()
                {
                    Timesent = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now),
                };
            }
            payload = AddUplinkMetrics(uplink, payload);

            return payload;
        }

        public void OnMQTTClientConnected(object sender, EventArgs e)
        {
            if (!nbirth_sent)
            {
                Payload payload = new Payload()
                {
                    Seq = GetSeq(),
                    Timestamp = (ulong)DateTimeOffset.Now.ToUnixTimeSeconds()
                };

                //namespace/group_id/message_type/edge_node_id
                //$"spBv1.0/DTX/{SparkPlugMessageTypes.NBIRTH}/PowerPilotCS";
                var topic = string.Format(_publishTopic, SparkPlugMessageTypes.NBIRTH, "").TrimEnd('/');
                MessageProcessed?.Invoke(this, new MQTTMessageArgs(topic, payload.ToByteArray()));

                nbirth_sent = true;
            }
        }

        public async Task HandleCommand(Payload cmdPayload, PowerPilotCommon.Models.PostgreSQL.Connection connection)
        {
            foreach(Payload.Types.Metric metric in cmdPayload.Metrics)
            {
                if (metric.Name == $"{DeviceCommands.TYPE}/{DeviceCommands.REBIRTH}")
                {
                    var downlink = await BuildDBIRTH(connection);
                    MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DBIRTH, connection.DeviceEUI), downlink.ToByteArray()));
                }
            }
            
        }

        public async void ProcessMessage(object sender, MQTTMessageArgs args)
        {
            try
            {
                string[] parts = args.Topic.Split("/");
                string deviceEUI = null;
                string messageType = null;
                if (parts[0] == "application")
                {
                    // application/powerpilot/uplink/{type}/{deviceEUI}
                    messageType = parts[3];
                    deviceEUI = parts[4];
                }
                else if (parts[0] == "spBv1.0")
                {
                    // spBv1.0/DTX/{0}/PowerPilotCS/{1}
                    messageType = parts[2];
                    deviceEUI = parts[4];
                }

                if (deviceEUI == null)
                {
                    _loggerClient.LogError($"Could not get device eui from {args.Topic}", "ProcessMessage", Pplogger.ErrorMessage.Types.Severity.Fatal);
                    return;
                }

                // check if this device exists in our system
                var connection = await _dataService.GetConnection(deviceEUI);

                if (connection == null)
                {
                    return;
                }

                Payload payload = new Payload()
                {
                    Seq = GetSeq(),
                    Timestamp = (ulong)DataServiceClient.ConvertToTimestamp(DateTime.Now)
                };

                // check if we have this deviceEUI already, if not then send DBIRTH
                // use lock to avoid concurrent adds
                bool added = false;
                lock(dbirthLock)
                {
                    if (!deviceEUIs.Contains(deviceEUI))
                    {
                        deviceEUIs.Add(deviceEUI);
                        added = true;
                    }
                }

                if (added)
                {
                    var dbirthPayload = await BuildDBIRTH(connection);
                    MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DBIRTH, connection.DeviceEUI), dbirthPayload.ToByteArray()));
                    // return;
                }

                // var topic = $"spBv1.0/DTX/{SparkPlugMessageTypes.DDATA}/PowerPilotCS/{deviceEUI}";
                switch (messageType)
                {
                    // sparkplug commands
                    case SparkPlugMessageTypes.DCMD:
                        var cmd = Payload.Parser.ParseFrom(args.Payload);
                        await HandleCommand(cmd, connection);
                        break;

                    // powerpilot messages
                    case TopicNames.ENERGY:
                        var energy = Ppuplink.EnergyMessage.Parser.ParseFrom(args.Payload);
                        var energyPayload = AddEnergyMetrics(energy, payload);
                        MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DDATA, deviceEUI), energyPayload.ToByteArray()));
                        break;

                    case TopicNames.PQ:
                        var pq = Ppuplink.PQMessage.Parser.ParseFrom(args.Payload);
                        var pqPayload = AddPQMetrics(pq, payload, connection);
                        MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DDATA, deviceEUI), pqPayload.ToByteArray()));
                        break;

                    case TopicNames.INST:
                        var inst = Ppuplink.InstMessage.Parser.ParseFrom(args.Payload);
                        // not interested yet
                        break;

                    case TopicNames.ALARM:
                        var alarm = Ppuplink.AlarmMessage.Parser.ParseFrom(args.Payload);
                        var alarmPayload = AddAlarmMetrics(alarm, payload, connection);
                        MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DDATA, deviceEUI), alarmPayload.ToByteArray()));

                        //if power fail also need DDEATH
                        if (alarm.Alarmtype == "powerfailalarm")
                        {
                            var ddeathPayload = BuildDDEATH(alarm);
                            var ddeath_topic = string.Format(_publishTopic, SparkPlugMessageTypes.DDEATH, deviceEUI);// $"spBv1.0/DTX/{SparkPlugMessageTypes.DDEATH}/PowerPilotCS/{deviceEUI}";
                            MessageProcessed?.Invoke(this, new MQTTMessageArgs(ddeath_topic, ddeathPayload.ToByteArray()));
                        }
                        //if normal voltage then also need DBIRTH
                        else if (alarm.Alarmtype == "normalvoltage")
                        {
                            var dbirthPayload = await BuildDBIRTH(connection);
                            MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DBIRTH, connection.DeviceEUI), dbirthPayload.ToByteArray()));
                        }

                        break;

                    case TopicNames.UPLINK:
                        var uplink = Ppuplink.UplinkMessage.Parser.ParseFrom(args.Payload);
                        var uplinkPayload = AddUplinkMetrics(uplink, payload);
                        MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DDATA, deviceEUI), uplinkPayload.ToByteArray()));
                        break;

                    case TopicNames.VOLTAGE_STATS:
                        var vs = Ppuplink.VoltageStatsMessage.Parser.ParseFrom(args.Payload);
                        var vsPayload = AddVoltageStatsMetrics(vs, payload, connection);
                        MessageProcessed?.Invoke(this, new MQTTMessageArgs(string.Format(_publishTopic, SparkPlugMessageTypes.DDATA, deviceEUI), vsPayload.ToByteArray()));
                        break;

                    default:
                        _loggerClient.LogMessage($"Not processing type {messageType}");
                        break;

                }
            }
            catch (Exception ex)
            {
                _loggerClient.LogError($"Exception while processing Message: {ex.Message}", "ProcessMessage", Pplogger.ErrorMessage.Types.Severity.Fatal);
                if (ex.StackTrace != null)
                {
                    _loggerClient.LogMessage(ex.StackTrace);
                }
                return;
            }

        }

    }
}
