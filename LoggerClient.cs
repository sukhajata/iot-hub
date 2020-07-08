using System;
using Grpc.Net.Client;
using Pplogger;
using static Pplogger.LoggerService;

namespace DeviceWifiToKafka
{
    public class LoggerClient
    {
        private static LoggerServiceClient client;
        private static readonly object padlock = new object();


        private static LoggerServiceClient GetClient()
        {
            lock (padlock)
            {
                if (client == null)
                {
                    var loggerAddress = Environment.GetEnvironmentVariable("loggerServiceAddress");
                    //allow insecure traffic
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                    var channel = GrpcChannel.ForAddress(loggerAddress);
                    client = new LoggerServiceClient(channel);
                }
            }
            return client;
        }

        public static void LogMessage(string msg)
        {
            string dd = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
            Console.WriteLine($"[{dd}]: {msg}");
        }


        public static void LogError(string msg, string functionName, ErrorMessage.Types.Severity severity = ErrorMessage.Types.Severity.Severe)
        {
            string dd = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
            Console.WriteLine($"[{dd}] {functionName}: {msg}");

            try
            {
                var request = new ErrorMessage
                {
                    Service = "ChirpstackToKafka",
                    Function = functionName,
                    Message = msg,
                    Severity = severity
                };
                GetClient().LogError(request, deadline: DateTime.UtcNow.AddSeconds(3));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send error message");
                Console.WriteLine(ex.Message);
            }
        }
    }
}
