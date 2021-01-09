using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinaryProtocolService;
using DeviceWifiToMosquitto.Services;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;

namespace DeviceWifiToMosquitto
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var envVars = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry ev in envVars)
            {
                Console.WriteLine("  {0} = {1}", ev.Key, ev.Value);
            }

            var mqttHostname = Environment.GetEnvironmentVariable("mqttHostname");
            var mqttPort = Int32.Parse(Environment.GetEnvironmentVariable("mqttPort"));
            var mqttUser = Environment.GetEnvironmentVariable("mqttUsername");
            var mqttPassword = Environment.GetEnvironmentVariable("mqttPassword");
            var uplinkTopic = Environment.GetEnvironmentVariable("uplinkTopic");
            var secure = Environment.GetEnvironmentVariable("secure");
            var binaryServiceAddress = Environment.GetEnvironmentVariable("binaryServiceAddress");
            var applicationID = Environment.GetEnvironmentVariable("applicationID");

            //logger
            var loggerService = new LoggerClient("DeviceWifiToMosquitto");

            //attempt to send test error message to wait for k8s start up DNS lag
            loggerService.LogError("test", "main", Pplogger.ErrorMessage.Types.Severity.Warning);

            //grpc binary protocol service
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); //allow insecure (within cluster)
            var channel = GrpcChannel.ForAddress(binaryServiceAddress);
            var binaryServiceClient = new BinaryProtocol.BinaryProtocolClient(channel);

            //mqtt client
            var random = Guid.NewGuid().ToString();
            string mqttClientId = "DeviceWifiToMosquito-" + random.Substring(0, 8) + "-" + applicationID;

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

            loggerService.LogMessage($"Connecting to {mqttHostname} on {mqttPort}");

            //managed client handles reconnecting if connection is lost
            var mqttClient = new MqttFactory().CreateManagedMqttClient();

            mqttClient.UseConnectedHandler(e =>
            {
                loggerService.LogMessage($"Connected to MQTT broker with result {e.AuthenticateResult.ResultCode} {e.AuthenticateResult.ReasonString}");
                Readiness.READY = true;
            });

            mqttClient.UseDisconnectedHandler(e =>
            {
                loggerService.LogMessage("Disconnected from MQTT brokers");
                Readiness.READY = false;
                if (e.Exception != null && e.Exception.Message != null)
                {
                    loggerService.LogMessage(e.Exception.Message);
                }
            });

            await mqttClient.StartAsync(managedOptions);

            //subscriber
            var mosquittoSubscriber = new MosquittoSubscriber(mqttClient, loggerService, uplinkTopic);

            //publisher
            var mosquittoPublisher = new MosquittoPublisher(mqttClient, loggerService);

            //message builder
            var messageBuilder = new MessageBuilder(binaryServiceClient, loggerService, mosquittoPublisher);

            //pipe events from one to the other
            mosquittoSubscriber.MessageReceived += messageBuilder.BuildMessage;

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
