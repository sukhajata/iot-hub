using System;
using PowerPilotCommon.Interfaces;
using Pplogger;

namespace MqttToSparkPlug.Tests.Mocks
{
    public class MockLogger : ILoggerClient
    {
        public MockLogger()
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
