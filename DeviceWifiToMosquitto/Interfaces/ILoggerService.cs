using System;
using Pplogger;

namespace DeviceWifiToMosquitto.Interfaces
{
    public interface ILoggerService
    {
        public void LogMessage(string msg);

        public void LogError(string msg, string functionName, ErrorMessage.Types.Severity severity = ErrorMessage.Types.Severity.Severe);
    }
}
