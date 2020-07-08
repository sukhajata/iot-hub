using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DeviceWifiToKafka
{
    public class Readiness : IHealthCheck
    {
        public static bool READY = false;


        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default(CancellationToken))
        {

            if (READY)
            {
                return Task.FromResult(
                    HealthCheckResult.Healthy("A healthy result."));
            }

            return Task.FromResult(
                HealthCheckResult.Unhealthy("An unhealthy result."));
        }
    }
}
