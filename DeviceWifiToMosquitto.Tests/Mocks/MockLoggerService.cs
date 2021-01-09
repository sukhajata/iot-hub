using System;
using Pplogger;
using DeviceWifiToMosquitto.Interfaces;

namespace DeviceWifiToMosquitto.Tests.Mocks
{
    public class MockLoggerService : ILoggerService
    {
        public MockLoggerService()
        {
        }

        public void LogError(string msg, string functionName, ErrorMessage.Types.Severity severity = ErrorMessage.Types.Severity.Severe)
        {
            Console.WriteLine(msg);
            throw new Exception(msg);
        }

        public void LogMessage(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
