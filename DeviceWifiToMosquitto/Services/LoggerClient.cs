using System;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using DeviceWifiToMosquitto.Interfaces;
using Pplogger;
using static Pplogger.LoggerService;

namespace DeviceWifiToMosquitto
{
    public class LoggerClient : ILoggerService
    {
        private static LoggerServiceClient client;
        private string _serviceName;

        public LoggerClient(string serviceName)
        {
            var loggerAddress = Environment.GetEnvironmentVariable("loggerServiceAddress");
            //allow insecure traffic
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var channel = GrpcChannel.ForAddress(loggerAddress);
            client = new LoggerServiceClient(channel);
            _serviceName = serviceName;
        }

        public void LogMessage(string msg)
        {
            string dd = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
            Console.WriteLine($"[{dd}]: {msg}");
        }

        public void LogError(string msg, string functionName, ErrorMessage.Types.Severity severity = ErrorMessage.Types.Severity.Severe)
        {
            string dd = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
            Console.WriteLine($"[{dd}] {functionName}: {msg}");

            var request = new ErrorMessage
            {
                Service = _serviceName,
                Function = functionName,
                Message = msg,
                Severity = severity
            };

            //try 5 times then die
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    client.LogError(request, deadline: DateTime.UtcNow.AddSeconds(3));
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Internal || ex.StatusCode == StatusCode.Unavailable)
                {
                    LogMessage("Failed to connect to logger service");
                    if (i == 4)
                    {
                        //kill me
                        Liveness.LIVE = false;
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    LogMessage("Failed to send error message");
                    LogMessage(ex.Message);
                }
            }

        }

    }
}
