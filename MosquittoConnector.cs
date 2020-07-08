using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using io.confluent.ksql.avro_schemas;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Extensions.ManagedClient;
using PowerPilot.Models;
using PowerPilot.Services.BinaryProtocol;
using PowerPilot.Services.Kafka;

namespace DeviceWifiToKafka
{
    public class MosquittoConnector
    {
        private IManagedMqttClient mqttClient;
        private static BinaryService binaryService;
        private static KafkaProducerService kafkaProducer;
        private readonly ILogger<MosquittoConnector> _logger;

        public MosquittoConnector()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<MosquittoConnector>();

            Task.Run(() => StartAsync());
        }

        public async Task StartAsync()
        {
            LoggerClient.LogMessage("Getting environment variables");
            var envVars = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry ev in envVars)
            {
                Console.WriteLine("  {0} = {1}", ev.Key, ev.Value);
            }

            var mqttHostname = Environment.GetEnvironmentVariable("mqttHostname");
            var mqttPort = Int32.Parse(Environment.GetEnvironmentVariable("mqttPort"));
            var mqttUser = Environment.GetEnvironmentVariable("mqttUsername");
            var mqttPassword = Environment.GetEnvironmentVariable("mqttPassword");
            var secure = Environment.GetEnvironmentVariable("secure");
            var applicationID = Environment.GetEnvironmentVariable("applicationID");

            //random client id
            var random = Guid.NewGuid().ToString();
            string mqttClientId = "DeviceWifiToKafka" + "-" + random.Substring(0, 8) + "-" + applicationID;

            binaryService = new BinaryService();

            KafkaProducerServiceConfiguration kafkaConfig = new KafkaProducerServiceConfiguration
            {
                INSTMSG = Environment.GetEnvironmentVariable("TOPIC_NAME_INSTMSG"),
                HARMONICS_LOWER = Environment.GetEnvironmentVariable("TOPIC_NAME_HARMONICS_LOWER"),
                HARMONICS_UPPER = Environment.GetEnvironmentVariable("TOPIC_NAME_HARMONICS_UPPER"),
                PROCESSEDMSG = Environment.GetEnvironmentVariable("TOPIC_NAME_PROCESSEDMSG"),
                UPLINK = Environment.GetEnvironmentVariable("TOPIC_NAME_UPLINKMSG"),
                GATEWAYRX = Environment.GetEnvironmentVariable("TOPIC_NAME_GATEWAYMSG"),
                ENERGY = Environment.GetEnvironmentVariable("TOPIC_NAME_ENERGYMSG"),
                ALARM = Environment.GetEnvironmentVariable("TOPIC_NAME_ALARMMSG"),
                CONFIG_REPORTED = Environment.GetEnvironmentVariable("TOPIC_NAME_CONFIG_UPLINK"),
                GEOSCAN_REPORTED = Environment.GetEnvironmentVariable("TOPIC_NAME_GEOSCANMSG"),
                POWER_QUALITY = Environment.GetEnvironmentVariable("TOPIC_NAME_PQMSG"),
                S11_POWER_QUALITY = Environment.GetEnvironmentVariable("TOPIC_NAME_S11PQMSG"),
                POWER_QUALITY_EVENTS = Environment.GetEnvironmentVariable("TOPIC_NAME_PQEVENTSMSG"),
                TIME_SYNC = Environment.GetEnvironmentVariable("TOPIC_NAME_TIMESYNC"),
                VOLTAGE_STATS = Environment.GetEnvironmentVariable("TOPIC_NAME_VOLTAGESTATSMSG"),
                HV_ALARM = Environment.GetEnvironmentVariable("TOPIC_NAME_HV_ALARM"),
                RESEND_RESPONSE = Environment.GetEnvironmentVariable("TOPIC_NAME_RESEND_RESPONSE"),
                CIRCUIT_ENERGY = Environment.GetEnvironmentVariable("TOPIC_NAME_CIRCUIT_ENERGY"),
                CIRCUIT_LOAD = Environment.GetEnvironmentVariable("TOPIC_NAME_CIRCUIT_LOAD"),
                PHASE2PHASE_INST = Environment.GetEnvironmentVariable("TOPIC_NAME_P2P_INST"),
                BOOTSTRAP_SERVERS_URL = Environment.GetEnvironmentVariable("kafkaBootstrap"),
                SCHEMA_REGISTRY_URL = Environment.GetEnvironmentVariable("schemaRegistry")
            };

            kafkaProducer = new KafkaProducerService();
            kafkaProducer.InitService(_logger, kafkaConfig);

            var options = new MqttClientOptionsBuilder()
                  .WithClientId(mqttClientId)
                  .WithCredentials(mqttUser, mqttPassword)
                  .WithTcpServer(mqttHostname, mqttPort)
                  .WithKeepAlivePeriod(TimeSpan.FromMinutes(20))
                  .WithCleanSession();

            if (secure == "true")
            {
                var tlsparams = new MqttClientOptionsBuilderTlsParameters();
                tlsparams.UseTls = true;
                tlsparams.AllowUntrustedCertificates = true;
                tlsparams.IgnoreCertificateChainErrors = true;
                tlsparams.IgnoreCertificateRevocationErrors = true;

                options.WithTls(tlsparams);

            }
            
            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options)
                .Build();

            LoggerClient.LogMessage($"Connecting to {mqttHostname} on {mqttPort}");
            //managed client handles reconnecting if connection is lost
            mqttClient = new MqttFactory().CreateManagedMqttClient();

            //subscriptions are managed across reconnections
            string topic = "powerpilot/device/+/rx";
            //string topic = "application/" + applicationID + "/device/+/rx";
            //var topicFilter = new MqttTopicFilter() { Topic = topic };
            //await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
            //await mqttClient.SubscribeAsync(new MqttTopicFilter[] { topicFilter });

            mqttClient.UseConnectedHandler(e =>
            {
                LoggerClient.LogMessage($"Connected to MQTT broker with result {e.AuthenticateResult.ResultCode} {e.AuthenticateResult.ReasonString}");
                Readiness.READY = true;
            });

            mqttClient.UseDisconnectedHandler(e =>
            {
                LoggerClient.LogMessage("Disconnected from MQTT brokers");
                Readiness.READY = false;
                if (e.Exception != null && e.Exception.Message != null)
                {
                    LoggerClient.LogMessage(e.Exception.Message);
                }
            });

            mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                try
                {
                    string topic = e.ApplicationMessage.Topic;
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    LoggerClient.LogMessage($"Topic: {topic}. Message Received: {payload}");

                    Task.Run(() => MessageReceived(payload));
                    //await MessageReceived(payload);
                }
                catch (Exception ex)
                {
                    LoggerClient.LogError(ex.Message, "StartAsync");
                }
            });

            mqttClient.SynchronizingSubscriptionsFailedHandler = new SynchronizingSubscriptionsFailedHandlerDelegate(OnSynchronizingSubscriptionsFailed);

            await mqttClient.StartAsync(managedOptions);
            await mqttClient.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);

        }

        public static void OnSynchronizingSubscriptionsFailed(ManagedProcessFailedEventArgs e)
        {
            LoggerClient.LogError(e.Exception.Message, "OnSynchronizingSubscriptionsFailed");
        }

        private static long ToUnixTime(string timestamp)
        {
            DateTime date = DateTime.Parse(timestamp);
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date - epoch).TotalMilliseconds);
        }

        private static byte[] String2Byte(String hexString)
        {
            List<byte> data = new List<byte>();
            for (int i = 0; i < hexString.Length; i += 2)
            {
                string hs = hexString.Substring(i, 2);
                data.Add(Convert.ToByte(hs, 16));
            }
            return data.ToArray();
        }


        private async void MessageReceived(string jsonString)
        {
            try
            {
                DeviceWifiMessage msg = DeviceWifiMessage.FromJson(jsonString);
                if (msg == null)
                {
                    return;
                }
                else
                {

                    string deviceEUI = msg.DeviceEUI.Trim();
                    if (msg.Data != null)
                    {
                        PowerPilotMessage ppMessage = await binaryService.DecodeAsync(String2Byte(msg.Data));

                        var uplink = new Uplink();
                        uplink.snr = 0;
                        uplink.fCnt = 0;
                        uplink.dr = 0;
                        uplink.frequency = 0;
                        uplink.rawdata = msg.Data;
                        uplink.timestamp = ToUnixTime(DateTime.Now.ToString());
                        uplink.messagetype = Convert.ToUInt16(msg.Data.Substring(0, 2), 16);
                        uplink.messageid = Convert.ToUInt16(msg.Data.Substring(2, 2), 16);
                        uplink.resent = (int)((Convert.ToUInt16(msg.Data.Substring(4, 2), 16) & 0x0040) == 0x0040 ? 1 : 0);
                        ppMessage.UplinkInfo = uplink;

                        ppMessage.GatewayInfo = new GatewayRX[] { };

                        await kafkaProducer.ProduceMessageAsync(deviceEUI, ppMessage);
                    }

                }
            }
            catch (Exception ex)
            {
                LoggerClient.LogError(ex.Message + ":" + ex.StackTrace, "MessageReceived");
            }

        }

    }
}
