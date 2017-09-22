namespace NServiceBus
{
    using System;
    using Metrics.ServiceControl;

    public static class MetricsOptionsExtensions
    {
        public static void SendMetricDataToServiceControl2(this MetricsOptions options, string serviceControlMetricsAddress, TimeSpan interval, string instanceId = null)
        {
            Guard.AgainstNullAndEmpty(nameof(serviceControlMetricsAddress), serviceControlMetricsAddress);
            Guard.AgainstNegativeAndZero(nameof(interval), interval);

            var reporting = ReportingOptions.Get(options);

            reporting.ServiceControlMetricsAddress = serviceControlMetricsAddress;
            reporting.ServiceControlReportingInterval = interval;
            reporting.EndpointInstanceIdOverride = instanceId;
        }
    }
}