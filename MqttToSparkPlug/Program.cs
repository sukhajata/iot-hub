using System;
using System.Threading.Tasks;
using MqttToSparkPlug.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Client.Options;
using MQTTnet.Client.Connecting;
using System.Net.Http;
using PowerPilotCommon;
using PowerPilotCommon.Services;
using PowerPilotCommon.Models.SparkPlug;

namespace MqttToSparkPlug
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            Environment.SetEnvironmentVariable("mqttPowerpilotHostname", "mosquitto");
            Environment.SetEnvironmentVariable("mqttPowerpilotPort", "8883");
            Environment.SetEnvironmentVariable("mqttPowerpilotUser", "superAdmin");
            Environment.SetEnvironmentVariable("mqttPowerpilotPassword", "powerpilot");
            Environment.SetEnvironmentVariable("powerpilotSecure", "true");
            Environment.SetEnvironmentVariable("powerpilotTopic", "application/powerpilot/uplink/#");

            Environment.SetEnvironmentVariable("mqttSparkplugHostname", "mosquitto");
            Environment.SetEnvironmentVariable("mqttSparkplugPort", "8883");
            Environment.SetEnvironmentVariable("mqttSparkplugUser", "superAdmin");
            Environment.SetEnvironmentVariable("mqttSparkplugPassword", "powerpilot");
            Environment.SetEnvironmentVariable("sparkplugSecure", "true");
            Environment.SetEnvironmentVariable("sparkplugTopic", "spBv1.0/DTX/{0}/PowerPilotCS/{1}");

            Environment.SetEnvironmentVariable("binaryServiceAddress", "http://binaryprotocol-service:5000");
            Environment.SetEnvironmentVariable("loggerServiceAddress", "http://logger-service:9031");

            Environment.SetEnvironmentVariable("dataServiceUrl", "https://data.devpower.powerpilot.nz");
            Environment.SetEnvironmentVariable("dataServiceToken", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoid2ViX3VzZXIiLCJ1c2VyIjoidGltYyJ9.eKUu_Iw3bk-5oxj7HAmOE4QZjd0rNsyXUWQpDB7QYUA");

            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                string name = (string)env.Key;
                string value = (string)env.Value;
                if (name.StartsWith("mqtt") || name.StartsWith("subscribe") || name.StartsWith("sparkplug"))
                {
                    Console.WriteLine("{0}={1}", name, value);
                }
            }

            var mqttPowerpilotHostname = Environment.GetEnvironmentVariable("mqttPowerpilotHostname");
            var mqttPowerpilotPort = Int32.Parse(Environment.GetEnvironmentVariable("mqttPowerpilotPort"));
            var mqttPowerpilotUser = Environment.GetEnvironmentVariable("mqttPowerpilotUser");
            var mqttPowerpilotPassword = Environment.GetEnvironmentVariable("mqttPowerpilotPassword");
            var powerpilotSecure = Environment.GetEnvironmentVariable("powerpilotSecure");
            var powerpilotTopic = Environment.GetEnvironmentVariable("powerpilotTopic");

            var mqttSparkplugHostname = Environment.GetEnvironmentVariable("mqttSparkplugHostname");
            var mqttSparkplugPort = Int32.Parse(Environment.GetEnvironmentVariable("mqttSparkplugPort"));
            var mqttSparkplugUser = Environment.GetEnvironmentVariable("mqttSparkplugUser");
            var mqttSparkplugPassword = Environment.GetEnvironmentVariable("mqttSparkplugPassword");
            var sparkplugSecure = Environment.GetEnvironmentVariable("sparkplugSecure");
            var sparkplugTopic = Environment.GetEnvironmentVariable("sparkplugTopic");

            var dataServiceUrl = Environment.GetEnvironmentVariable("dataServiceUrl");
            var dataServiceToken = Environment.GetEnvironmentVariable("dataServiceToken");

            // logger
            var loggerAddress = Environment.GetEnvironmentVariable("loggerServiceAddress");
            var loggerClient = new LoggerClient(loggerAddress, "MqttToSparkPlug");

            // mqtt client for receiving powerpilot format messages
            var mqttPowerpilotWrapper = await MQTTClientWrapperFactory.GetNewMQTTClientWrapper(mqttPowerpilotUser, mqttPowerpilotPassword, mqttPowerpilotHostname, mqttPowerpilotPort, true, loggerClient);
            await mqttPowerpilotWrapper.Subscribe(powerpilotTopic);

            // mqtt client for sending sparkplug format messages
            var mqttSparkplugWrapper = await MQTTClientWrapperFactory.GetNewMQTTClientWrapper(mqttSparkplugUser, mqttSparkplugPassword, mqttSparkplugHostname, mqttSparkplugPort, true, loggerClient);
            // subscribe to commands - spBv1.0/DTX/DCMD/PowerPilotCS/+
            await mqttSparkplugWrapper.Subscribe(string.Format(sparkplugTopic, SparkPlugMessageTypes.DCMD, "+"));

            // data service client
            HttpClient httpClient = new HttpClient();
            var dataServiceClient = new DataServiceClient(httpClient, dataServiceUrl, dataServiceToken);

            // sparkplug generator
            var sparkPlugGenerator = new SparkPlugGenerator(loggerClient, dataServiceClient, sparkplugTopic);

            // pipe messages
            mqttSparkplugWrapper.MQTTConnected += sparkPlugGenerator.OnMQTTClientConnected;
            mqttPowerpilotWrapper.MessageReceived += sparkPlugGenerator.ProcessMessage;
            sparkPlugGenerator.MessageProcessed += mqttSparkplugWrapper.Publish;

            // commands
            mqttSparkplugWrapper.MessageReceived += sparkPlugGenerator.ProcessMessage;

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
